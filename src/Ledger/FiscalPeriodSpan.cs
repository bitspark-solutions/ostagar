namespace Ostagar.Ledger;

/// <summary>
/// A period as the calendar derives it, before it becomes a row.
/// </summary>
/// <remarks>
/// Separate from <see cref="FiscalPeriod"/> so the calendar stays a pure function with no database
/// concerns, and so provisioning can generate a year, check it, and only then persist it. Locks are
/// deliberately absent here: they are state a controller sets later, not something the calendar owns.
/// </remarks>
/// <param name="Number">1 through 12 within the fiscal year.</param>
/// <param name="FiscalYear">The year this period belongs to, which is not always its start year.</param>
/// <param name="StartDate">First day, inclusive.</param>
/// <param name="EndDate">Last day, inclusive.</param>
/// <param name="Name">Display label.</param>
/// <param name="NameBangla">Bangla display label for printed reports.</param>
public readonly record struct FiscalPeriodSpan(
    int Number,
    int FiscalYear,
    DateOnly StartDate,
    DateOnly EndDate,
    string Name,
    string NameBangla)
{
    /// <summary>
    /// Whether a posting date falls inside this period. Endpoints are inclusive on both sides, which
    /// matters because a period boundary is exactly where a backdated entry goes wrong.
    /// </summary>
    public bool Contains(DateOnly date) => StartDate <= date && date <= EndDate;
}
