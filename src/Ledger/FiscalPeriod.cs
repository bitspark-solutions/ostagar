using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// One accounting period, with the three locks that decide what may still be written into it.
/// </summary>
/// <remarks>
/// The legacy system had no period concept at all: no fiscal year, no close, no lock date, and an
/// <c>OpeningBalance</c> column that nothing read. Without a lock there is no such thing as a closed
/// book, so an accountant cannot sign off on anything and a backdated entry can quietly change a
/// period whose VAT return has already been filed.
///
/// The three tiers follow Odoo, because they solve three different problems:
/// <list type="bullet">
/// <item><description><b>Soft lock</b> — non-advisers may not post; an accountant still can, because
/// adjustments after a return are normal and blocking them pushes people into editing live rows.</description></item>
/// <item><description><b>Hard lock</b> — nobody posting through the application. Overridable only by
/// direct database access, which is the point.</description></item>
/// <item><description><b>Tax lock</b> — the filing boundary, so a return can be reproduced exactly.</description></item>
/// </list>
/// </remarks>
public class FiscalPeriod : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>1 through 12, counted from <see cref="StartDate"/> rather than from January.</summary>
    public int Number { get; set; }

    /// <summary>The fiscal year this period belongs to, e.g. 2026 for July 2026 – June 2027.</summary>
    public int FiscalYear { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>Postings up to and including this date need the adviser role. Null means unset.</summary>
    public DateOnly? SoftLockedOn { get; set; }

    /// <summary>Postings up to and including this date are refused to everyone.</summary>
    public DateOnly? HardLockedOn { get; set; }

    /// <summary>Postings up to and including this date are refused because the return is filed.</summary>
    public DateOnly? TaxLockedOn { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Bangla period label, eashrey 1433 and similar, for printed reports.</summary>
    public string NameBangla { get; set; } = string.Empty;

    /// <summary>
    /// Whether <paramref name="date"/> may still be posted to by an ordinary user. The check runs
    /// inside the posting transaction, not in the UI: a client-side warning is a courtesy, and the
    /// legacy tree had date rules in three places that disagreed with each other.
    /// </summary>
    public bool AcceptsPosting(DateOnly date) =>
        date >= StartDate
        && date <= EndDate
        && (!SoftLockedOn.HasValue || date > SoftLockedOn.Value)
        && (!HardLockedOn.HasValue || date > HardLockedOn.Value)
        && (!TaxLockedOn.HasValue || date > TaxLockedOn.Value);
}
