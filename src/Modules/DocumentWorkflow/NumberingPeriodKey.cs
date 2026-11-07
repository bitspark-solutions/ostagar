using System.Globalization;
using Ostagar.Ledger;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The one-period key a document-number counter belongs to, derived from the tenant's fiscal calendar.
/// </summary>
/// <remarks>
/// Derived through <see cref="Ostagar.Ledger.FiscalCalendar"/> rather than from the date's own year,
/// because a July-start fiscal year spans two calendar years: a delivery note dated January 2027
/// belongs to fiscal year 2026. Deriving the period from <c>date.Year</c> would silently start the
/// 2027 counter three periods early, so two documents would carry the same number in a period the
/// tenant believes is the same one.
///
/// An empty <see cref="Code"/> is the <see cref="NumberingPeriodComponent.None"/> case: one sequence
/// that never restarts, which still needs a key so that the sequence row has a primary value.
/// </remarks>
public readonly record struct NumberingPeriodKey
{
    public NumberingPeriodKey(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        Code = code.Trim();
    }

    /// <summary>The invariant, machine-readable period, e.g. <c>2026</c> or <c>2026-07</c>.</summary>
    public string Code { get; }

    /// <summary>True for the never-restarting sequence.</summary>
    public bool IsEmpty => Code.Length == 0;

    /// <summary>
    /// The period a document dated <paramref name="date"/> belongs to.
    /// </summary>
    /// <param name="date">The document date, i.e. the date printed on the paper, not today.</param>
    /// <param name="component">How the tenant splits this document type's sequence.</param>
    /// <param name="fiscalStartMonth">The tenant's fiscal-year start month; 7 for Bangladesh.</param>
    public static NumberingPeriodKey Of(DateOnly date, NumberingPeriodComponent component, int fiscalStartMonth) =>
        component switch
        {
            NumberingPeriodComponent.None => new NumberingPeriodKey(string.Empty),
            NumberingPeriodComponent.FiscalYear => new NumberingPeriodKey(
                FiscalCalendar.FiscalYearOf(date, fiscalStartMonth).ToString("D4", CultureInfo.InvariantCulture)),
            NumberingPeriodComponent.FiscalMonth => new NumberingPeriodKey(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{FiscalCalendar.FiscalYearOf(date, fiscalStartMonth):D4}-{FiscalCalendar.PeriodNumberOf(date, fiscalStartMonth):D2}")),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown numbering period component."),
        };
}
