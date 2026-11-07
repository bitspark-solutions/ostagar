using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// What a tenant may and may not change on a numbering scheme that is already in use — above all, that a
/// narrower padding width is refused rather than answered with two documents sharing one number.
/// </summary>
public class NumberingSchemeEditValidatorTests
{
    private static readonly TenantId Tenant = TenantId.From(
        Guid.Parse("33333333-3333-3333-3333-333333333333"));

    private static readonly Guid SchemeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Narrowing_the_padding_after_a_number_has_been_issued_is_refused()
    {
        var current = Scheme(width: 4);
        var proposed = Scheme(width: 3);

        var decision = NumberingSchemeEditValidator.Validate(
            current,
            proposed,
            [Issued(nextValue: 4)]);

        Assert.False(decision.IsAccepted);
        Assert.Contains(NumberingErrorCode.WidthNarrowedAfterIssue, decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void Widening_the_padding_is_allowed_because_no_issued_value_can_be_re_rendered_as_an_existing_number()
    {
        var current = Scheme(width: 4);
        var proposed = Scheme(width: 5);

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, [Issued(nextValue: 20)]);

        Assert.True(decision.IsAccepted);
        Assert.Empty(decision.Errors);
    }

    [Fact]
    public void Changing_the_prefix_after_issue_is_refused_because_the_paper_already_carries_it()
    {
        var current = Scheme(prefix: "SO");
        var proposed = Scheme(prefix: "SORDER");

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, [Issued(nextValue: 4)]);

        Assert.Contains(
            NumberingErrorCode.PrefixChangedAfterIssue,
            decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void Changing_the_period_span_after_issue_is_refused_because_it_reopens_closed_periods()
    {
        var current = Scheme(component: NumberingPeriodComponent.FiscalYear);
        var proposed = Scheme(component: NumberingPeriodComponent.FiscalMonth);

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, [Issued(nextValue: 4)]);

        Assert.Contains(
            NumberingErrorCode.PeriodComponentChangedAfterIssue,
            decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_scheme_that_has_issued_nothing_can_still_be_corrected()
    {
        var current = Scheme(width: 4);
        var proposed = Scheme(width: 3, prefix: "INV");

        var opened = DocumentNumberAllocator.OpenSequence(
            current,
            NumberingPeriodKey.Of(new DateOnly(2026, 10, 1), current.PeriodComponent, current.FiscalStartMonth),
            Tenant) with { Id = SchemeId };

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, [opened]);

        Assert.True(decision.IsAccepted);
    }

    [Fact]
    public void A_width_that_cannot_render_the_counter_is_refused_even_when_the_width_only_grew()
    {
        var current = Scheme(width: 3);
        var proposed = Scheme(width: 4);

        // Five digits already reached on this counter, four digits proposed. Nothing has been issued in
        // this particular row, but the width still cannot render it.
        var decision = NumberingSchemeEditValidator.Validate(
            current,
            proposed,
            [Issued(nextValue: 100_000, issuedCount: 0)]);

        Assert.Contains(
            NumberingErrorCode.WidthNarrowerThanCounter,
            decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void Every_shape_defect_in_a_proposal_is_reported_in_one_pass()
    {
        var current = Scheme();
        var proposed = new DocumentNumberingScheme
        {
            Id = SchemeId,
            TenantId = Tenant,
            DocumentType = DocumentTypeCode.SalesOrder,
            Prefix = "sales orders",
            Width = 0,
            StartingValue = 0,
            FiscalStartMonth = 13,
        };

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, []);

        var codes = decision.Errors.Select(error => error.Code).ToList();

        Assert.Contains(NumberingErrorCode.InvalidPrefix, codes);
        Assert.Contains(NumberingErrorCode.WidthOutOfRange, codes);
        Assert.Contains(NumberingErrorCode.StartingValueBelowOne, codes);
        Assert.Contains(NumberingErrorCode.InvalidFiscalStartMonth, codes);
    }

    [Fact]
    public void Counters_of_other_schemes_do_not_constrain_this_edit()
    {
        var current = Scheme(width: 4);
        var proposed = Scheme(width: 3);

        var foreign = Issued(nextValue: 900) with { SchemeId = Guid.Parse("55555555-5555-5555-5555-555555555555") };

        var decision = NumberingSchemeEditValidator.Validate(current, proposed, [foreign]);

        Assert.True(decision.IsAccepted);
    }

    private static NumberingSequenceState Issued(int nextValue, int issuedCount = 3) =>
        new()
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            TenantId = Tenant,
            SchemeId = SchemeId,
            DocumentType = DocumentTypeCode.SalesOrder,
            PeriodKey = "2026",
            NextValue = nextValue,
            IssuedCount = issuedCount,
            LastAllocationKey = "SALES_ORDER:1:1",
            LastAllocatedNumber = $"SO-2026-{(nextValue - 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture)}",
        };

    private static DocumentNumberingScheme Scheme(
        string prefix = "SO",
        int width = 4,
        NumberingPeriodComponent component = NumberingPeriodComponent.FiscalYear) =>
        new()
        {
            Id = SchemeId,
            TenantId = Tenant,
            DocumentType = DocumentTypeCode.SalesOrder,
            Prefix = prefix,
            Width = width,
            PeriodComponent = component,
            StartingValue = 1,
            FiscalStartMonth = 7,
        };
}
