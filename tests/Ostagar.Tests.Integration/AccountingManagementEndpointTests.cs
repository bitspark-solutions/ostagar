using System.Net;
using System.Net.Http.Json;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Rbac;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the accounting-management endpoints over real HTTP: periods can be listed and locked, the
/// trial balance ties over real postings, exchange rates record and resolve, and the founding
/// opening-balance entry can be written through the API.
/// </summary>
/// <remarks>
/// Same standard as the journal suite: a 201 is not proof — every write is read back through the API
/// or the harness's own store, so the row the response claims is the row that exists.
/// </remarks>
public sealed class AccountingManagementEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_signed_in_user_can_list_the_twelve_provisioned_periods()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, "/api/v1/ledger/periods"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var periods = await response.Content.ReadFromJsonAsync<List<PeriodRow>>();
        Assert.NotNull(periods);
        Assert.Equal(12, periods.Count);
        Assert.Equal(1, periods[0].Number);
        Assert.Equal(2026, periods[0].FiscalYear);
        Assert.All(periods, each => Assert.Null(each.SoftLockedOn));
    }

    [Fact]
    public async Task A_soft_lock_set_over_http_refuses_postings_into_the_period()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);
        var period = await FirstPeriodAsync(company);

        var locked = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/periods/{period.Id}/locks",
            new { Level = "soft", On = period.EndDate }));

        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);

        var after = await FirstPeriodAsync(company);
        Assert.Equal(period.EndDate, after.SoftLockedOn);

        // The lock is real where it matters: the same posting that succeeded before is now refused by
        // the ledger itself, which is the only meaning "closed" has.
        var posting = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(company.World)));

        Assert.Equal(HttpStatusCode.BadRequest, posting.StatusCode);
        Assert.Contains("PeriodLocked", await posting.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lock_cannot_move_backwards_and_the_denial_is_audited()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);
        var period = await FirstPeriodAsync(company);

        await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/periods/{period.Id}/locks",
            new { Level = "hard", On = period.EndDate }));

        var backwards = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/periods/{period.Id}/locks",
            new { Level = "hard", On = period.EndDate.AddDays(-5) }));

        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains("lock_moves_backwards", await backwards.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var after = await FirstPeriodAsync(company);
        Assert.Equal(period.EndDate, after.HardLockedOn);

        var audit = await ledger.AuditRowsAsync(company.World);
        Assert.Contains(audit, row => row.Action == "ledger.period.lock" && row.Outcome == AuditOutcome.Allowed);
        Assert.Contains(audit, row => row.Action == "ledger.period.lock" && row.Outcome == AuditOutcome.Denied);
    }

    [Fact]
    public async Task A_lock_date_outside_the_period_is_refused()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);
        var period = await FirstPeriodAsync(company);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/periods/{period.Id}/locks",
            new { Level = "soft", On = period.EndDate.AddDays(1) }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("lock_date_outside_period", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_trial_balance_ties_over_a_real_posting_and_names_the_accounts()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);
        var period = await FirstPeriodAsync(company);

        var posted = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(company.World)));
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/ledger/trial-balance?periodId={period.Id}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trial = await response.Content.ReadFromJsonAsync<TrialBalance>();
        Assert.NotNull(trial);
        Assert.True(trial.IsBalanced);
        Assert.Equal(500m, trial.TotalDebit);
        Assert.Equal(500m, trial.TotalCredit);
        Assert.Equal(2, trial.Rows.Count);

        var cash = trial.Rows.Single(each => each.Code == "1010");
        Assert.Equal(500m, cash.TotalDebit);
        Assert.Equal(0m, cash.TotalCredit);
        Assert.Equal(500m, cash.Balance);

        var income = trial.Rows.Single(each => each.Code == "4010");
        Assert.Equal(0m, income.TotalDebit);
        Assert.Equal(500m, income.TotalCredit);
        Assert.Equal(500m, income.Balance);
    }

    [Fact]
    public async Task The_trial_balance_refuses_an_unbounded_read()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, "/api/v1/ledger/trial-balance"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_recorded_rate_resolves_for_a_later_posting_date()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);

        var recorded = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/ledger/exchange-rates",
            new { Currency = "USD", ValidFrom = new DateOnly(2026, 7, 1), Rate = 120m, Source = "bank letter" }));

        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        var resolved = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, "/api/v1/ledger/exchange-rates/resolve?currency=USD&date=2026-07-15"));

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var quote = await resolved.Content.ReadFromJsonAsync<ResolvedRate>();
        Assert.NotNull(quote);
        Assert.Equal(120m, quote.Rate);
        Assert.Equal(new DateOnly(2026, 7, 1), quote.ValidFrom);
    }

    [Fact]
    public async Task A_rate_for_the_functional_currency_is_refused()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/ledger/exchange-rates",
            new { Currency = "BDT", ValidFrom = new DateOnly(2026, 7, 1), Rate = 1m, Source = "test" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rate_refused", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Opening_balances_post_the_founding_entry_through_the_api()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Owner);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/ledger/opening-balances",
            new
            {
                FiscalYear = 2026,
                Balances = new object[]
                {
                    new { AccountCode = "1010", Amount = 1000m },
                    new { AccountCode = "3010", Amount = 1000m },
                },
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreatedEntry>();
        Assert.NotNull(created);
        Assert.Contains(created.Number, await ledger.EntryNumbersAsync(company.World));
    }

    [Fact]
    public async Task A_clerk_without_the_owner_role_cannot_move_a_lock()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Stock);
        var periods = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, "/api/v1/ledger/periods"));

        Assert.Equal(HttpStatusCode.OK, periods.StatusCode);

        var period = (await periods.Content.ReadFromJsonAsync<List<PeriodRow>>())![0];

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/periods/{period.Id}/locks",
            new { Level = "hard", On = period.EndDate }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<PeriodRow> FirstPeriodAsync(HarnessCompany company)
    {
        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, "/api/v1/ledger/periods"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var periods = await response.Content.ReadFromJsonAsync<List<PeriodRow>>();
        Assert.NotNull(periods);
        return periods[0];
    }

    private static object BalancedBody(LedgerWorld world) => new
    {
        JournalCode = "GL",
        PostingDate = new DateOnly(2026, 7, 1),
        Memo = "written by the management suite",
        Lines = new object[]
        {
            new { AccountId = world.Cash, Debit = 500m, Credit = (decimal?)null, Memo = "cash in" },
            new { AccountId = world.Income, Debit = (decimal?)null, Credit = 500m, Memo = "revenue" },
        },
    };

    private sealed record PeriodRow(
        Guid Id,
        int Number,
        int FiscalYear,
        DateOnly EndDate,
        DateOnly? SoftLockedOn,
        DateOnly? HardLockedOn,
        DateOnly? TaxLockedOn);

    private sealed record TrialBalanceRow(string Code, decimal TotalDebit, decimal TotalCredit, decimal Balance);

    private sealed record TrialBalance(List<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool IsBalanced);

    private sealed record ResolvedRate(string Currency, DateOnly ValidFrom, decimal Rate);

    private sealed record CreatedEntry(Guid EntryId, string Number);
}
