using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// The numbering rules most likely to be subtly wrong, tested before a single document exists to break:
/// no gap, no duplicate under a retry, no silent restart at a period boundary.
/// </summary>
public class DocumentNumberAllocatorTests
{
    private static readonly TenantId Tenant = TenantId.From(
        Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly Guid SchemeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Allocates_consecutive_numbers_with_no_gap()
    {
        var scheme = Scheme();
        var sequence = DocumentNumberAllocator.OpenSequence(
            scheme, NumberingPeriodKey.Of(October2026, scheme.PeriodComponent, scheme.FiscalStartMonth), Tenant);

        var first = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:1:1", On = October2026, ExpectedNextValue = 1 });
        var second = DocumentNumberAllocator.Allocate(
            scheme,
            first.Sequence!,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:2:1", On = October2026, ExpectedNextValue = 2 });

        Assert.Equal(NumberingDecisionStatus.Allocated, first.Status);
        Assert.Equal("SO-2026-0001", first.Number);
        Assert.Equal("SO-2026-0002", second.Number);
        Assert.Equal(3, second.Sequence!.NextValue);
        Assert.Equal(2, second.Sequence.IssuedCount);
    }

    [Fact]
    public void A_retry_of_the_same_allocation_key_returns_the_same_number_and_consumes_nothing()
    {
        var scheme = Scheme();
        var sequence = OpenFor(scheme, October2026);

        var first = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:7:1", On = October2026, ExpectedNextValue = 1 });

        // The page timed out after the row was written and the clerk pressed the button again. The row has
        // since advanced, so the caller's expected value is stale — and the replay rule must be checked
        // first, or the retry would be refused instead of returning the number the document already has.
        var retry = DocumentNumberAllocator.Allocate(
            scheme,
            first.Sequence!,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:7:1", On = October2026, ExpectedNextValue = 1 });

        Assert.Equal(NumberingDecisionStatus.Replayed, retry.Status);
        Assert.Equal(first.Number, retry.Number);
        Assert.Equal(first.Sequence!.NextValue, retry.Sequence!.NextValue);
        Assert.Equal(first.Sequence.IssuedCount, retry.Sequence.IssuedCount);
    }

    [Fact]
    public void Two_concurrent_readers_do_not_both_get_a_number()
    {
        var scheme = Scheme();
        var sequence = OpenFor(scheme, October2026);

        var won = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:8:1", On = October2026, ExpectedNextValue = 1 });
        var loser = DocumentNumberAllocator.Allocate(
            scheme,
            won.Sequence!,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:9:1", On = October2026, ExpectedNextValue = 1 });

        Assert.Equal(NumberingDecisionStatus.Allocated, won.Status);
        Assert.Equal(NumberingDecisionStatus.Refused, loser.Status);
        Assert.Contains(NumberingErrorCode.StaleSequenceRead, loser.Errors.Select(error => error.Code));

        // A refusal must not advance the counter, or the refused document's retry lands two numbers ahead.
        Assert.Null(loser.Sequence);
        Assert.Equal(2, won.Sequence!.NextValue);
    }

    [Fact]
    public void A_cancelled_document_keeps_the_number_it_consumed_and_the_next_document_gets_the_next_one()
    {
        var scheme = Scheme();
        var sequence = OpenFor(scheme, October2026);

        var cancelled = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:10:1", On = October2026 });
        var successor = DocumentNumberAllocator.Allocate(
            scheme,
            cancelled.Sequence!,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:11:1", On = October2026 });

        Assert.Equal("SO-2026-0001", cancelled.Number);
        Assert.Equal("SO-2026-0002", successor.Number);

        // The series is whole even though one of its two documents no longer exists commercially. There is
        // no Release method to call, which is the enforcement: a gap created by deletion is
        // indistinguishable from a document that was never registered at all.
        var report = NumberingSequenceInspector.Inspect(
            scheme,
            NumberingPeriodKey.Of(October2026, scheme.PeriodComponent, scheme.FiscalStartMonth),
            [cancelled.Number, successor.Number]);

        Assert.True(report.IsGapless);
    }

    [Fact]
    public void A_counter_that_outgrows_its_padding_is_refused_rather_than_truncated_or_widened()
    {
        var scheme = Scheme(width: 2);
        var sequence = OpenFor(scheme, October2026) with { NextValue = 100, IssuedCount = 99 };

        var decision = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:12:1", On = October2026 });

        Assert.Equal(NumberingDecisionStatus.Refused, decision.Status);
        Assert.Contains(NumberingErrorCode.ValueOutgrowsPadding, decision.Errors.Select(error => error.Code));

        // Truncating to the width would print 100 as "00", the form already carried by document 100 in a
        // ten-thousand-strong series. The renderer refuses instead, so the only way past the width is an
        // explicit, auditable widening of the scheme.
        Assert.False(NumberingFormat.Fits(2, 100));

        var period = PeriodOf(scheme, October2026);
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberingFormat.Render(scheme.Prefix, period, 2, 100));
    }

    [Fact]
    public void A_new_fiscal_year_needs_its_own_counter_and_is_not_rolled_over_silently()
    {
        var scheme = Scheme();
        var fiscal2026 = OpenFor(scheme, October2026);
        var october2027 = new DateOnly(2027, 10, 1);

        var mismatch = DocumentNumberAllocator.Allocate(
            scheme,
            fiscal2026,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:13:1", On = october2027 });

        Assert.Equal(NumberingDecisionStatus.Refused, mismatch.Status);
        Assert.Contains(NumberingErrorCode.SequencePeriodMismatch, mismatch.Errors.Select(error => error.Code));

        var fresh = DocumentNumberAllocator.OpenSequence(scheme, PeriodOf(scheme, october2027), Tenant);
        var allocated = DocumentNumberAllocator.Allocate(
            scheme,
            fresh,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:13:1", On = october2027 });

        Assert.Equal("SO-2027-0001", allocated.Number);
    }

    [Fact]
    public void A_january_document_is_numbered_in_the_fiscal_year_it_began_in_july()
    {
        var scheme = Scheme();
        var january2027 = new DateOnly(2027, 1, 15);

        var sequence = OpenFor(scheme, january2027);
        var allocated = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:14:1", On = january2027 });

        // July-start: January 2027 is still fiscal year 2026. Deriving the period from the date's own year
        // would have started a 2027 series three months early and put two live documents on one number.
        Assert.Equal("SO-2026-0001", allocated.Number);
    }

    [Fact]
    public void A_month_component_numbers_by_fiscal_period_not_calendar_period()
    {
        var scheme = Scheme(component: NumberingPeriodComponent.FiscalMonth);
        var sequence = OpenFor(scheme, October2026);

        var allocated = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "DELIVERY_NOTE:1:1", On = October2026 });

        // October is the fourth period of a July-start year.
        Assert.Equal("SO-2026-04-0001", allocated.Number);
    }

    [Fact]
    public void A_counter_rewound_below_its_series_start_is_refused()
    {
        var scheme = Scheme(startingValue: 5);
        var sequence = OpenFor(scheme, October2026) with { NextValue = 3, IssuedCount = 90 };

        var decision = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:15:1", On = October2026 });

        Assert.Equal(NumberingDecisionStatus.Refused, decision.Status);
        Assert.Contains(
            NumberingErrorCode.CounterBelowStartingValue,
            decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void An_allocation_without_a_key_is_refused_because_a_retry_could_not_be_recognised()
    {
        var scheme = Scheme();
        var sequence = OpenFor(scheme, October2026);

        var decision = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "   ", On = October2026 });

        Assert.Equal(NumberingDecisionStatus.Refused, decision.Status);
        Assert.Contains(NumberingErrorCode.AllocationKeyEmpty, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void An_inactive_scheme_refuses_to_register_documents()
    {
        var scheme = Scheme();
        var sequence = OpenFor(scheme, October2026);

        scheme.IsActive = false;

        var refused = DocumentNumberAllocator.Allocate(
            scheme,
            sequence,
            new NumberingAllocationRequest { AllocationKey = "SALES_ORDER:16:1", On = October2026 });

        Assert.Equal(NumberingDecisionStatus.Refused, refused.Status);
        Assert.Contains(NumberingErrorCode.SchemeInactive, refused.Errors.Select(error => error.Code));
    }

    private static readonly DateOnly October2026 = new(2026, 10, 1);

    private static NumberingPeriodKey PeriodOf(DocumentNumberingScheme scheme, DateOnly on) =>
        NumberingPeriodKey.Of(on, scheme.PeriodComponent, scheme.FiscalStartMonth);

    private static NumberingSequenceState OpenFor(DocumentNumberingScheme scheme, DateOnly on) =>
        DocumentNumberAllocator.OpenSequence(scheme, PeriodOf(scheme, on), Tenant);

    private static DocumentNumberingScheme Scheme(
        string prefix = "SO",
        int width = 4,
        NumberingPeriodComponent component = NumberingPeriodComponent.FiscalYear,
        int startingValue = 1) =>
        new()
        {
            Id = SchemeId,
            TenantId = Tenant,
            DocumentType = DocumentTypeCode.SalesOrder,
            Name = "Sales orders",
            NameBangla = "বিক্রয় আদেশ",
            Prefix = prefix,
            Width = width,
            PeriodComponent = component,
            StartingValue = startingValue,
            FiscalStartMonth = 7,
        };
}
