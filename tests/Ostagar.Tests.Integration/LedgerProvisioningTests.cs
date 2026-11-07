using Ostagar.Ledger;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Provisions a company through the product's own path and checks the books it ends up with.
/// </summary>
/// <remarks>
/// Onboarding is where an ERP project usually quietly fails: the demo company was hand-created in a
/// database console, so nobody notices that the code to create one has never run. These tests only ever
/// use <see cref="ILedgerProvisioner"/>, and the numbers they assert are the ones that make a first
/// posting possible — twelve periods, a hierarchy the roll-ups can walk, and a journal for opening
/// balances, which the legacy build declared in an enum and then never used.
/// </remarks>
public sealed class LedgerProvisioningTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task Provisioning_creates_all_twelve_periods_of_the_fiscal_year()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        Assert.Equal(12, await ledger.PeriodCountAsync(world, 2026));
    }

    [Fact]
    public async Task Period_one_of_a_July_year_starts_on_the_first_of_July()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var (periodNumber, postingDate) = await ledger.EntryPlacementAsync(world, await FirstEntryIdAsync(world));

        // Asserted on a real posting rather than on the period row alone, because a period whose dates
        // are right but which the engine resolves to the wrong month fails the same way users do.
        Assert.Equal(1, periodNumber);
        Assert.Equal(new DateOnly(2026, 7, 1), postingDate);
        Assert.Equal(new DateOnly(2026, 7, 1), world.PostingDate);
    }

    [Fact]
    public async Task Grouping_accounts_are_created_non_postable_and_leaves_are_not()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var accounts = await ledger.AccountsAsync(world);

        var groups = accounts.Where(each => !each.IsPostable).ToList();

        Assert.NotEmpty(groups);
        Assert.All(groups, each => Assert.Null(each.ParentId));
        Assert.All(accounts.Where(each => each.IsPostable), each => Assert.NotNull(each.ParentId));
    }

    [Fact]
    public async Task Every_leaf_paths_under_its_group_so_roll_ups_can_find_it()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var accounts = (await ledger.AccountsAsync(world))
            .ToDictionary(each => each.Code, each => each.Path, StringComparer.Ordinal);

        // The ltree column is only useful if the materialised path actually nests. A leaf stored as its
        // own bare code is invisible to every ancestor query, so its balance reaches no roll-up while
        // still appearing in the flat trial balance — the kind of wrong that reports as a discrepancy
        // between two screens rather than as an error.
        Assert.Equal("100", accounts["100"]);
        Assert.Equal("100.1010", accounts["1010"]);
        Assert.Equal("700.7020", accounts["7020"]);
    }

    [Fact]
    public async Task The_standard_journals_include_one_for_opening_balances()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var codes = await ledger.JournalCodesAsync(world);

        Assert.Equal(
            ["BANK", "CASH", "GL", "OPEN", "PUR", "SALE"],
            codes);

        Assert.Contains(StandardJournals.OpeningCode, codes);
    }

    [Fact]
    public async Task Provisioning_the_same_year_twice_adds_nothing()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var accountsBefore = (await ledger.AccountsAsync(world)).Count;
        var periodsBefore = await ledger.PeriodCountAsync(world, 2026);
        var journalsBefore = (await ledger.JournalCodesAsync(world)).Count;

        await ledger.ProvisionAgainAsync(world, 2026);

        Assert.Equal(accountsBefore, (await ledger.AccountsAsync(world)).Count);
        Assert.Equal(periodsBefore, await ledger.PeriodCountAsync(world, 2026));
        Assert.Equal(journalsBefore, (await ledger.JournalCodesAsync(world)).Count);
    }

    [Fact]
    public async Task Opening_the_next_year_adds_periods_without_rewriting_the_chart()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.ProvisionAgainAsync(world, 2027);

        Assert.Equal(12, await ledger.PeriodCountAsync(world, 2026));
        Assert.Equal(12, await ledger.PeriodCountAsync(world, 2027));

        // Two years of periods, one chart of exactly the template's size. A provisioner that re-ran the
        // accounts per year would collide with the unique code constraint and fail loudly rather than
        // silently double the chart, so the count is asserted against the template rather than itself.
        Assert.Equal(BaseChartOfAccounts.Accounts.Count, await AccountCountAsync(world));
        Assert.Equal(StandardJournals.Journals.Count, (await ledger.JournalCodesAsync(world)).Count);
    }

    [Fact]
    public async Task One_company_gets_its_own_accounts_rather_than_the_previous_company_s()
    {
        var first = await ledger.ProvisionedCompanyAsync(2026);
        var second = await ledger.ProvisionedCompanyAsync(2026);

        var firstCash = (await ledger.AccountsAsync(first)).Single(each => each.Code == "1010");
        var secondCash = (await ledger.AccountsAsync(second)).Single(each => each.Code == "1010");

        Assert.Equal(firstCash.Code, secondCash.Code);

        // Same code, different rows. A shared chart would be the most expensive tenancy bug available:
        // every company's postings would land in one set of accounts and the trial balance would report
        // the whole platform as if it were one business.
        Assert.NotEqual(firstCash.Id, secondCash.Id);
        Assert.NotEqual(firstCash.ParentId, secondCash.ParentId);
    }

    private async Task<int> AccountCountAsync(LedgerWorld world) => (await ledger.AccountsAsync(world)).Count;

    private async Task<Guid> FirstEntryIdAsync(LedgerWorld world)
    {
        var outcome = await ledger.PostAsync(world, amount: 250m);

        Assert.True(outcome.Succeeded, string.Join("; ", outcome.Errors.Select(each => each.Message)));

        return outcome.EntryId!.Value;
    }

    private async Task<int> CountAsync(LedgerWorld world) => (await ledger.AccountsAsync(world)).Count;
}
