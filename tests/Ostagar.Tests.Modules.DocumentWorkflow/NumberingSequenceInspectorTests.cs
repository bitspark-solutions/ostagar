using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// The audit-facing half of numbering: proving from the numbers on the documents alone that a period's
/// series is whole, including the documents that were cancelled.
/// </summary>
public class NumberingSequenceInspectorTests
{
    private static readonly TenantId Tenant = TenantId.From(
        Guid.Parse("77777777-7777-7777-7777-777777777777"));

    private static readonly DocumentNumberingScheme Scheme = new()
    {
        Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
        TenantId = Tenant,
        DocumentType = DocumentTypeCode.SalesInvoice,
        Prefix = "INV",
        Width = 4,
        PeriodComponent = NumberingPeriodComponent.FiscalYear,
        StartingValue = 1,
        FiscalStartMonth = 7,
    };

    private static readonly NumberingPeriodKey Period2026 = new("2026");

    [Fact]
    public void Finds_the_range_a_tampered_series_left_out()
    {
        var report = NumberingSequenceInspector.Inspect(
            Scheme,
            Period2026,
            ["INV-2026-0001", "INV-2026-0002", "INV-2026-0003", "INV-2026-0005"]);

        Assert.False(report.IsGapless);
        Assert.Single(report.MissingRanges);
        Assert.Equal(new NumberingValueRange(4, 4), report.MissingRanges[0]);
        Assert.Equal(1, report.MissingRanges[0].Count);
    }

    [Fact]
    public void Reports_the_missing_span_at_the_start_of_a_series()
    {
        var report = NumberingSequenceInspector.Inspect(Scheme, Period2026, ["INV-2026-0003"]);

        Assert.Equal(new NumberingValueRange(1, 2), report.MissingRanges[0]);
    }

    [Fact]
    public void Reports_a_number_carried_by_two_documents()
    {
        var report = NumberingSequenceInspector.Inspect(
            Scheme,
            Period2026,
            ["INV-2026-0001", "INV-2026-0002", "INV-2026-0002"]);

        Assert.False(report.IsGapless);
        Assert.Contains("INV-2026-0002", report.DuplicateNumbers);
    }

    [Fact]
    public void Refuses_to_believe_a_number_somebody_typed_in()
    {
        var report = NumberingSequenceInspector.Inspect(
            Scheme,
            Period2026,
            ["INV-2026-1", "INV2026-0004", "INV-2026-0002", "INV-2027-0002", "  "]);

        Assert.False(report.IsGapless);

        // One readable value out of five. Each unreadable form is something this scheme could not have
        // rendered: wrong padding, missing separator, wrong period, no number at all.
        Assert.Equal(4, report.UnreadableNumbers.Count);
        Assert.Equal(2, report.LowestValue);
        Assert.Equal(2, report.HighestValue);
    }

    [Fact]
    public void A_whole_series_that_includes_cancelled_documents_is_gapless()
    {
        // The cancelled invoice keeps its number, so the series is still contiguous. A report that skipped
        // cancelled documents would report its absence as a gap, which is the confusion between "deleted"
        // and "never registered" that the no-gap rule exists to forbid.
        var report = NumberingSequenceInspector.Inspect(
            Scheme,
            Period2026,
            ["INV-2026-0001", "INV-2026-0002", "INV-2026-0003"]);

        Assert.True(report.IsGapless);
        Assert.Empty(report.MissingRanges);
        Assert.Empty(report.DuplicateNumbers);
        Assert.Equal(3, report.HighestValue);
    }

    [Fact]
    public void An_empty_period_is_whole_rather_than_missing_everything()
    {
        var report = NumberingSequenceInspector.Inspect(Scheme, Period2026, []);

        Assert.True(report.IsGapless);
        Assert.Equal(0, report.LowestValue);
    }

    [Fact]
    public void A_scheme_that_cannot_render_numbers_cannot_be_used_for_an_audit()
    {
        var broken = new DocumentNumberingScheme
        {
            Id = Scheme.Id,
            TenantId = Tenant,
            DocumentType = DocumentTypeCode.SalesInvoice,
            Prefix = "lower case",
            Width = 99,
            PeriodComponent = NumberingPeriodComponent.FiscalYear,
            StartingValue = 0,
            FiscalStartMonth = 7,
        };

        Assert.Throws<ArgumentException>(() =>
            NumberingSequenceInspector.Inspect(broken, Period2026, ["INV-2026-0001"]));
    }
}
