using System.Globalization;

namespace Ostagar.Ledger;

/// <summary>
/// Derives fiscal periods from a tenant's fiscal-year start month.
/// </summary>
/// <remarks>
/// Every tenant in v1 starts in July, matching the Bangladeshi fiscal year, but the start month is a
/// parameter rather than a constant because numbering and locking are both keyed on period and a later
/// tenant with a calendar year must not require a schema change.
///
/// The subtle part, and the reason this is its own tested class rather than a loop in a service: a
/// July-start year spans two calendar years, so <c>FiscalYear</c> cannot be derived from the period's
/// own start date. A period dated January 2027 belongs to fiscal year 2026. Getting that wrong shifts
/// every year-over-year comparison by one and looks entirely plausible while doing it.
/// </remarks>
public static class FiscalCalendar
{
    /// <summary>Bangladesh fiscal year: 1 July to 30 June.</summary>
    public const int DefaultStartMonth = 7;

    private static readonly CultureInfo Bangla = new("bn-BD");

    /// <summary>
    /// The fiscal year a date belongs to, given a start month. A date in the months before the start
    /// month belongs to the year that began in the previous calendar year.
    /// </summary>
    public static int FiscalYearOf(DateOnly date, int startMonth)
    {
        ValidateStartMonth(startMonth);

        return date.Month >= startMonth ? date.Year : date.Year - 1;
    }

    /// <summary>Which period number (1-12) a date falls in within its fiscal year.</summary>
    public static int PeriodNumberOf(DateOnly date, int startMonth)
    {
        ValidateStartMonth(startMonth);

        return ((date.Month - startMonth + 12) % 12) + 1;
    }

    /// <summary>
    /// Builds the twelve periods of one fiscal year, first and last inclusive.
    /// </summary>
    /// <remarks>
    /// Twelve only. A 13-period year exists in some accounting traditions and the schema keeps
    /// <c>number</c> as a foreign reference rather than baking twelve in, so adding it later does not
    /// require altering the ledger tables — which invariant I3 makes close to impossible to do safely.
    /// </remarks>
    public static IReadOnlyList<FiscalPeriodSpan> YearOf(int fiscalYear, int startMonth, string? namePrefix = null)
    {
        ValidateStartMonth(startMonth);

        var periods = new List<FiscalPeriodSpan>(12);
        var start = new DateOnly(fiscalYear, startMonth, 1);

        for (var index = 0; index < 12; index++)
        {
            var periodStart = start.AddMonths(index);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var number = index + 1;

            periods.Add(new FiscalPeriodSpan(
                number,
                fiscalYear,
                periodStart,
                periodEnd,
                Name: string.IsNullOrEmpty(namePrefix)
                    ? periodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                    : $"{namePrefix}-{number:00}",
                NameBangla: periodStart.ToString("MMMM yyyy", Bangla)));
        }

        return periods;
    }

    /// <summary>
    /// Finds the period a posting date belongs to within a generated year, or null if the date falls
    /// outside it.
    /// </summary>
    public static FiscalPeriodSpan? PeriodContaining(IReadOnlyList<FiscalPeriodSpan> periods, DateOnly date)
    {
        foreach (var period in periods)
        {
            if (period.Contains(date))
            {
                return period;
            }
        }

        return null;
    }

    private static void ValidateStartMonth(int startMonth)
    {
        if (startMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startMonth),
                "The fiscal year must start in a month between 1 and 12.");
        }
    }
}
