using Ostagar.Ledger;

namespace Ostagar.Tests.Unit;

public class FiscalCalendarTests
{
    private const int July = FiscalCalendar.DefaultStartMonth;

    [Fact]
    public void A_july_start_year_begins_in_july()
    {
        var periods = FiscalCalendar.YearOf(2026, July);

        Assert.Equal(12, periods.Count);
        Assert.Equal(new DateOnly(2026, 7, 1), periods[0].StartDate);
        Assert.Equal(new DateOnly(2026, 7, 31), periods[0].EndDate);
        Assert.Equal(1, periods[0].Number);
    }

    [Fact]
    public void The_year_ends_on_thirty_june_of_the_following_calendar_year()
    {
        var periods = FiscalCalendar.YearOf(2026, July);
        var last = periods[11];

        Assert.Equal(12, last.Number);
        Assert.Equal(new DateOnly(2027, 6, 1), last.StartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), last.EndDate);
    }

    [Fact]
    public void A_date_in_the_early_calendar_year_belongs_to_the_previous_fiscal_year()
    {
        // The trap this exists to catch: January 2027 is fiscal 2026 for a July-start year. Deriving
        // the fiscal year from the period's own start date shifts every year-over-year comparison by
        // one, and the reports still look plausible.
        Assert.Equal(2026, FiscalCalendar.FiscalYearOf(new DateOnly(2027, 1, 15), July));
        Assert.Equal(2026, FiscalCalendar.FiscalYearOf(new DateOnly(2027, 6, 30), July));
        Assert.Equal(2027, FiscalCalendar.FiscalYearOf(new DateOnly(2027, 7, 1), July));
    }

    [Theory]
    [InlineData(2026, 7, 1)]
    [InlineData(2026, 8, 2)]
    [InlineData(2026, 12, 6)]
    [InlineData(2027, 1, 7)]
    [InlineData(2027, 6, 12)]
    public void Period_numbers_count_from_the_start_month(int year, int month, int expectedNumber)
    {
        Assert.Equal(expectedNumber, FiscalCalendar.PeriodNumberOf(new DateOnly(year, month, 1), July));
    }

    [Fact]
    public void Periods_are_contiguous_and_cover_the_whole_year_including_february_in_a_leap_year()
    {
        var periods = FiscalCalendar.YearOf(2026, July);

        for (var index = 1; index < periods.Count; index++)
        {
            Assert.Equal(
                periods[index - 1].EndDate.AddDays(1),
                periods[index].StartDate);
        }

        // February 2027 is not a leap month; February 2028 is. The 29th must land in a period.
        var following = FiscalCalendar.YearOf(2027, July);
        var february = following.First(period => period.StartDate.Month == 2);

        Assert.Equal(new DateOnly(2028, 2, 29), february.EndDate);
        Assert.True(february.Contains(new DateOnly(2028, 2, 29)));
    }

    [Fact]
    public void A_date_outside_the_year_matches_no_period()
    {
        var periods = FiscalCalendar.YearOf(2026, July);

        Assert.Null(FiscalCalendar.PeriodContaining(periods, new DateOnly(2026, 6, 30)));
        Assert.Null(FiscalCalendar.PeriodContaining(periods, new DateOnly(2027, 7, 1)));
        Assert.NotNull(FiscalCalendar.PeriodContaining(periods, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void A_calendar_year_start_is_also_representable()
    {
        var periods = FiscalCalendar.YearOf(2026, startMonth: 1);

        Assert.Equal(new DateOnly(2026, 1, 1), periods[0].StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), periods[11].EndDate);
        Assert.Equal(2026, FiscalCalendar.FiscalYearOf(new DateOnly(2026, 12, 31), startMonth: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void An_impossible_start_month_is_refused(int startMonth) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FiscalCalendar.YearOf(2026, startMonth));
}
