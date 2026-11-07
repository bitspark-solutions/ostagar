using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// A tenant's rule for numbering one document type: a prefix, a fixed width, a period component and
/// the value a new period starts from.
/// </summary>
/// <remarks>
/// This is configuration — the part a tenant edits — and it is deliberately separate from
/// <see cref="NumberingSequenceState"/>, which is the counter itself. Merging them, as the legacy
/// <c>GenerateJournalNumberAsync</c> effectively did by reading the current maximum back out of the
/// documents table, is what makes numbering rules drift: the next value then depends on which rows
/// happen to be visible, and a document that is deleted lowers the maximum for everyone after it.
///
/// <see cref="Width"/> is a promise about printed length, not decoration. See
/// <see cref="NumberingSchemeEditValidator"/>: a narrower width re-renders the same value into a
/// shorter string, and the only ways to keep a fixed length are to truncate — which re-issues a number
/// that already exists — or to let the number silently grow, which is how <c>JE-2025-01-9</c> came to
/// sort above <c>JE-2025-01-10</c> in the legacy voucher list.
/// </remarks>
public sealed class DocumentNumberingScheme : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>The one document type this scheme numbers.</summary>
    public required DocumentTypeCode DocumentType { get; set; }

    /// <summary>Settings-screen label, English.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Settings-screen label, Bangla. Required for the same reason every label is.</summary>
    public string NameBangla { get; set; } = string.Empty;

    /// <summary>Literal part first in every number, e.g. <c>SO</c>.</summary>
    public required string Prefix { get; set; }

    /// <summary>Minimum digits in the numeric part. Never reduced once a number has been issued.</summary>
    public int Width { get; set; } = 4;

    /// <summary>How the sequence is split across the tenant's calendar.</summary>
    public NumberingPeriodComponent PeriodComponent { get; set; } = NumberingPeriodComponent.FiscalYear;

    /// <summary>First value issued in a period. One, unless a tenant is continuing a paper series.</summary>
    public int StartingValue { get; set; } = 1;

    /// <summary>Fiscal-year start month, read from the tenant profile rather than assumed to be January.</summary>
    public int FiscalStartMonth { get; set; } = FiscalCalendar.DefaultStartMonth;

    /// <summary>
    /// An inactive scheme refuses allocation rather than disappearing, because the sequence it owns
    /// must stay explainable to an auditor who is asking about a document from two years ago.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
