using Ostagar.Ledger;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Posts an opening balance sheet the way it actually arrives: a list of accounts and amounts from
/// someone's previous books, rarely tying.
/// </summary>
/// <remarks>
/// <para>
/// The interesting behaviour is what happens when the two sides do not agree, because that is the normal
/// case rather than the exception. Refusing the import leaves an onboarding that cannot finish; silently
/// adjusting one of the owner's figures to force it leaves a balance sheet that is wrong in a place
/// nobody will look. So the difference is posted to owners capital and named — an explicit assumption a
/// person can see, disagree with, and correct.
/// </para>
///
/// <para>
/// The balances here are stated as signed amounts on each account's normal side, which is how a trial
/// balance is written. Nothing in these tests chooses a debit or a credit; that follows from the account
/// type, in the one place this project decides it.
/// </para>
/// </remarks>
public sealed class OpeningBalanceTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_balance_sheet_that_ties_posts_one_balanced_entry()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 40_000m),
            new OpeningBalance("1030", 260_000m),
            new OpeningBalance("2010", 150_000m),
            new OpeningBalance("3010", 150_000m),
        ]);

        Assert.Equal(PostingOutcomeStatus.Posted, outcome.Status);
        Assert.Equal("OPEN-000001", outcome.Number);

        var net = await ledger.NetByAccountAsync(world);

        Assert.Equal(40_000m, net[world.Cash]);
        Assert.Equal(150_000m, -net[await CapitalAccountIdAsync(world)]);
    }

    [Fact]
    public async Task The_difference_between_the_two_sides_lands_in_owners_capital_and_is_not_hidden()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        // Assets of 500,000 against liabilities of 100,000: the 400,000 has to be somebody's money, and
        // the only honest answer is the owner's until they say otherwise.
        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 100_000m),
            new OpeningBalance("1040", 400_000m),
            new OpeningBalance("2010", 100_000m),
        ]);

        Assert.True(outcome.Succeeded);

        var net = await ledger.NetByAccountAsync(world);
        var capital = await CapitalAccountIdAsync(world);

        Assert.Equal(500_000m, net[world.Cash] + net[await InventoryAccountIdAsync(world)]);
        Assert.Equal(-400_000m, net[capital]);
    }

    [Fact]
    public async Task A_credit_heavy_balance_sheet_debits_capital_instead_of_crediting_it()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 10_000m),
            new OpeningBalance("2010", 50_000m),
        ]);

        Assert.True(outcome.Succeeded);

        var net = await ledger.NetByAccountAsync(world);

        // Liabilities exceed assets, so capital is negative — the company owes more than it holds. The
        // ledger has to say that rather than round it toward zero, which is what a Math.abs in a render
        // path used to do here.
        Assert.Equal(40_000m, net[await CapitalAccountIdAsync(world)]);
    }

    [Fact]
    public async Task Balances_are_placed_by_account_type_not_by_which_side_the_importer_guessed()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 100_000m),
            new OpeningBalance("2010", 30_000m),
            new OpeningBalance("3010", 70_000m),
        ]);

        var net = await ledger.NetByAccountAsync(world);

        // Positive means "on the account's own normal side". Both a positive asset and a negative
        // liability read as positive here, which is the point: the sign describes the account, not the
        // column it was typed into.
        Assert.Equal(100_000m, net[world.Cash]);
        Assert.Equal(30_000m, -net[await PayableAccountIdAsync(world)]);
    }

    [Fact]
    public async Task An_overdraft_is_allowed_to_open_on_the_wrong_side_of_its_account()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", -5_000m),
            new OpeningBalance("3010", 5_000m),
        ]);

        Assert.True(outcome.Succeeded);

        var net = await ledger.NetByAccountAsync(world);

        // Cash is a debit-normal account holding a credit. Refusing this would mean a bank overdraft at
        // onboarding is unrepresentable, and the usual workaround is worse: dropping the row.
        Assert.Equal(-5_000m, net[world.Cash]);
    }

    [Fact]
    public async Task Every_unknown_code_is_named_in_one_refusal_not_one_per_attempt()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("9999", 100m),
            new OpeningBalance("8888", 200m),
        ]);

        Assert.Equal(PostingErrorCode.UnknownAccountCodes, outcome.Errors[0].Code);

        // An opening balance sheet is transcribed from paper. Reporting one misread code per attempt
        // turns twelve corrections into twelve round trips through the same document.
        Assert.Contains("9999", outcome.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("8888", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicated_account_code_is_refused_rather_than_added_together()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 1_000m),
            new OpeningBalance("1010", 1_000m),
            new OpeningBalance("3010", 2_000m),
        ]);

        // Whether the sheet means 2,000 or was counted twice has only one safe answer when the result is
        // the balance every later report is drawn from.
        Assert.Equal(PostingErrorCode.DuplicateAccountCode, outcome.Errors[0].Code);
    }

    [Fact]
    public async Task A_grouping_account_cannot_carry_a_balance()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("100", 100_000m),
            new OpeningBalance("3010", 100_000m),
        ]);

        Assert.Equal(PostingErrorCode.AccountUnpostable, outcome.Errors[0].Code);
        Assert.Contains("grouping account", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_balances_are_skipped_rather_than_posted_as_empty_lines()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 50_000m),
            new OpeningBalance("1020", 0m),
            new OpeningBalance("1040", 0m),
            new OpeningBalance("3010", 50_000m),
        ]);

        Assert.True(outcome.Succeeded);

        var net = await ledger.NetByAccountAsync(world);

        // A chart has more accounts than a company uses. Rows of 0.00 make the accounts that carry
        // anything harder to find, and they are the ones a closing balance sheet lists because the
        // spreadsheet that produced it had them in column A.
        Assert.Equal(2, net.Count);
    }

    [Fact]
    public async Task A_second_import_reports_the_original_entry_rather_than_posting_another_one()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var balances = new OpeningBalance[]
        {
            new("1010", 100_000m),
            new("3010", 100_000m),
        };

        var first = await ledger.PostOpeningBalancesAsync(world, 2026, balances);
        var again = await ledger.PostOpeningBalancesAsync(world, 2026, balances);

        // Onboarding a factory means importing from whatever the accountant used before, and that run
        // gets interrupted. Finishing it must not found the company twice.
        Assert.Equal(PostingOutcomeStatus.Posted, first.Status);
        Assert.Equal(PostingOutcomeStatus.AlreadyPosted, again.Status);
        Assert.Equal(first.EntryId, again.EntryId);
        Assert.Single(await ledger.EntryIdsAsync(world));
    }

    [Fact]
    public async Task Nothing_can_be_opened_before_the_company_has_a_fiscal_year()
    {
        var world = await ledger.ProvisionAsync();

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2031,
        [
            new OpeningBalance("1000", 100m),
            new OpeningBalance("4000", 100m),
        ]);

        Assert.Equal(PostingErrorCode.PeriodNotFound, outcome.Errors[0].Code);
        Assert.Contains("Provision", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_balance_sheet_is_refused_rather_than_read_as_starting_from_nothing()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026, []);

        // "No balances" and "no import ran" have to stay distinguishable in the log of a company being
        // onboarded; a zero-entry posting would silently look like either.
        Assert.Equal(PostingErrorCode.NoBalances, outcome.Errors[0].Code);
        Assert.Empty(await ledger.EntryIdsAsync(world));
    }

    [Fact]
    public async Task Opening_balances_reach_the_period_the_company_starts_in_not_the_current_one()
    {
        var world = await ledger.ProvisionedCompanyAsync(2026);

        var outcome = await ledger.PostOpeningBalancesAsync(world, 2026,
        [
            new OpeningBalance("1010", 100_000m),
            new OpeningBalance("3010", 100_000m),
        ]);

        var (periodNumber, postingDate) = await ledger.EntryPlacementAsync(world, outcome.EntryId!.Value);

        Assert.Equal(1, periodNumber);
        Assert.Equal(new DateOnly(2026, 7, 1), postingDate);
    }

    private async Task<Guid> CapitalAccountIdAsync(LedgerWorld world) =>
        await ledger.FindAccountByCodeAsync(world.TenantId, "3010");

    private async Task<Guid> PayableAccountIdAsync(LedgerWorld world) =>
        await ledger.FindAccountByCodeAsync(world.TenantId, "2010");

    private async Task<Guid> InventoryAccountIdAsync(LedgerWorld world) =>
        await ledger.FindAccountByCodeAsync(world.TenantId, "1040");
}
