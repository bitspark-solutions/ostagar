using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ostagar.Data;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;
using Ostagar.Platform.Tenancy;

namespace Ostagar.Tests.Integration;

public sealed class BankingEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Theory]
    [InlineData("BDT", 1)]
    [InlineData("USD", 120)]
    public async Task Bank_transfer_and_reconciliation_persist_and_retries_do_not_duplicate(string currency, decimal rate)
    {
        var harness = new LedgerHarness(factory);
        var company = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        using var scope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), company.World.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();
        var bankGl = await context.Accounts.SingleAsync(x => x.SubType == AccountSubType.Bank);
        var secondGl = new ChartOfAccount
        {
            Id = Guid.NewGuid(), Code = "1021", Name = "Second bank", Type = AccountType.Asset,
            SubType = AccountSubType.Bank, IsPostable = true, IsActive = true, Path = "second_bank",
        };
        context.Accounts.Add(secondGl);
        await context.SaveChangesAsync();
        if (currency != "BDT")
            await harness.RecordRateAsync(company.World, Ostagar.Core.CurrencyCode.From(currency), company.World.PostingDate, rate);
        var first = await CreateBankAsync(company, bankGl.Id, "FIRST", currency);
        var second = await CreateBankAsync(company, secondGl.Id, "SECOND", currency);
        var draft = new BankTransferDraft(Guid.NewGuid(), first, second, company.World.PostingDate, 125m, "Transfer");
        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/banking/transfers", draft));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = payload.GetProperty("entryId").GetGuid();
        var entry = await context.GlEntries.Include(x => x.Lines).SingleAsync(x => x.Id == entryId);
        Assert.Equal(StandardJournals.BankCode, entry.JournalCode);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(125m * rate, entry.Lines.Single(x => x.Debit != null).Debit);
        Assert.All(entry.Lines, x => Assert.Equal(125m, x.AmountInCurrency));
        var retry = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/banking/transfers", draft));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await context.GlEntries.CountAsync(x => x.SourceId == draft.Id));
        var line = entry.Lines.Single(x => x.AccountId == secondGl.Id);
        var statement = new BankReconciliationDraft(Guid.NewGuid(), draft.PostingDate, draft.PostingDate,
            0m, 125m, [line.Id]);
        var path = $"/api/v1/banking/accounts/{second}/reconciliations";
        var reconciled = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post, path, statement));
        Assert.Equal(HttpStatusCode.Created, reconciled.StatusCode);
        Assert.True(await context.BankReconciliationMatches.AnyAsync(x => x.GlLineId == line.Id));
        var again = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post, path, statement));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(1, await context.BankReconciliations.CountAsync(x => x.BankAccountId == second));
        var wrong = statement with { Id = Guid.NewGuid(), From = statement.To.AddDays(1), To = statement.To.AddDays(1), OpeningBalance = 125m, ClosingBalance = 250m };
        var doubleMatch = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post, path, wrong));
        Assert.Equal(HttpStatusCode.BadRequest, doubleMatch.StatusCode);
        var changed = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/banking/transfers", draft with { Amount = 126m }));
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        var view = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Get,
            $"/api/v1/banking/accounts/{second}/statement?asOf=2026-07-01"));
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var report = await view.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(125m, report.GetProperty("ledgerBalance").GetDecimal());
        Assert.Equal(0m, report.GetProperty("difference").GetDecimal());
        var forgedId = Guid.NewGuid();
        var actor = LedgerHarness.Actor;
        var date = statement.To.AddDays(1);
        var error = await LedgerHarness.StoreErrorAsync(async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO bank_reconciliations
                (id, tenant_id, bank_account_id, from_date, to_date, opening_balance, closing_balance, actor_user_id, created_at)
            VALUES ({forgedId}, {company.World.TenantId.Value}, {second}, {date}, {date}, 125, 999, {actor}, now())
            """));
        Assert.Contains("does not tie", error);
        Assert.False(await context.BankReconciliations.AnyAsync(x => x.Id == forgedId));
        var lateMatch = Guid.NewGuid();
        var lateError = await LedgerHarness.StoreErrorAsync(async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO bank_reconciliation_matches (id, tenant_id, reconciliation_id, gl_line_id)
            VALUES ({lateMatch}, {company.World.TenantId.Value}, {statement.Id}, {line.Id})
            """));
        Assert.Contains("created in this transaction", lateError);
        var immutableError = await LedgerHarness.StoreErrorAsync(async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bank_reconciliations SET closing_balance = 999 WHERE id = {statement.Id}"));
        Assert.NotNull(immutableError);
        var list = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Get,
            "/api/v1/banking/accounts"));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var immediateStatement = Guid.NewGuid();
        var unmatchedLine = entry.Lines.Single(x => x.AccountId == bankGl.Id).Id;
        var immediateMatch = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO bank_reconciliations
                (id, tenant_id, bank_account_id, from_date, to_date, opening_balance, closing_balance, actor_user_id, created_at)
            VALUES ({immediateStatement}, {company.World.TenantId.Value}, {first}, {draft.PostingDate}, {draft.PostingDate}, 0, 0, {actor}, now())
            """);
        await context.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE");
        var immediateError = await LedgerHarness.StoreErrorAsync(async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO bank_reconciliation_matches (id, tenant_id, reconciliation_id, gl_line_id)
            VALUES ({immediateMatch}, {company.World.TenantId.Value}, {immediateStatement}, {unmatchedLine})
            """));
        await transaction.RollbackAsync();
        Assert.Contains("does not tie", immediateError);
    }

    [Fact]
    public async Task Concurrent_identical_transfers_post_once_and_every_retry_succeeds()
    {
        var harness = new LedgerHarness(factory);
        var company = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        using var scope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), company.World.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();
        var bankGl = await context.Accounts.SingleAsync(x => x.SubType == AccountSubType.Bank);
        var secondGl = new ChartOfAccount
        {
            Id = Guid.NewGuid(), Code = "1022", Name = "Second bank", Type = AccountType.Asset,
            SubType = AccountSubType.Bank, IsPostable = true, IsActive = true, Path = "second_bank",
        };
        context.Accounts.Add(secondGl);
        await context.SaveChangesAsync();
        var first = await CreateBankAsync(company, bankGl.Id, "FIRST");
        var second = await CreateBankAsync(company, secondGl.Id, "SECOND");
        var draft = new BankTransferDraft(Guid.NewGuid(), first, second, company.World.PostingDate, 125m, "Transfer");
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
                "/api/v1/banking/transfers", draft))));
        var statuses = new List<string>();
        foreach (var attempt in attempts)
        {
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
            statuses.Add((await attempt.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()!);
        }
        Assert.Equal(1, statuses.Count(x => x == PostingOutcomeStatus.Posted.ToString()));
        Assert.Equal(3, statuses.Count(x => x == PostingOutcomeStatus.AlreadyPosted.ToString()));
        Assert.Equal(1, await context.GlEntries.CountAsync(x => x.SourceId == draft.Id));
    }

    [Fact]
    public async Task Concurrent_conflicting_transfers_post_once_and_the_loser_is_told()
    {
        var harness = new LedgerHarness(factory);
        var company = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        using var scope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), company.World.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();
        var bankGl = await context.Accounts.SingleAsync(x => x.SubType == AccountSubType.Bank);
        var secondGl = new ChartOfAccount
        {
            Id = Guid.NewGuid(), Code = "1023", Name = "Second bank", Type = AccountType.Asset,
            SubType = AccountSubType.Bank, IsPostable = true, IsActive = true, Path = "second_bank",
        };
        context.Accounts.Add(secondGl);
        await context.SaveChangesAsync();
        var first = await CreateBankAsync(company, bankGl.Id, "FIRST");
        var second = await CreateBankAsync(company, secondGl.Id, "SECOND");
        var id = Guid.NewGuid();
        var drafts = new[]
        {
            new BankTransferDraft(id, first, second, company.World.PostingDate, 125m, "Transfer"),
            new BankTransferDraft(id, first, second, company.World.PostingDate, 126m, "Transfer"),
        };
        var attempts = await Task.WhenAll(drafts.Select(draft =>
            company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
                "/api/v1/banking/transfers", draft))));
        var winnerIndex = attempts[0].StatusCode == HttpStatusCode.OK ? 0 : 1;
        Assert.Equal(HttpStatusCode.OK, attempts[winnerIndex].StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, attempts[1 - winnerIndex].StatusCode);
        var loserBody = await attempts[1 - winnerIndex].Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BankingErrorCode.RetryConflict.ToString(), loserBody.GetProperty("reason").GetString());
        var entry = await context.GlEntries.Include(x => x.Lines).SingleAsync(x => x.SourceId == id);
        Assert.Equal(drafts[winnerIndex].Amount, entry.Lines.Single(x => x.Debit != null).AmountInCurrency);
    }

    [Fact]
    public async Task A_line_in_a_foreign_currency_on_a_registered_bank_is_refused()
    {
        var harness = new LedgerHarness(factory);
        var company = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        using var scope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), company.World.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();
        var bankGl = await context.Accounts.SingleAsync(x => x.SubType == AccountSubType.Bank);
        var income = await context.Accounts.SingleAsync(x => x.Code == "4010");
        _ = await CreateBankAsync(company, bankGl.Id, "USDBANK", "USD");
        var manual = new
        {
            JournalCode = "GL",
            PostingDate = company.World.PostingDate,
            Memo = "foreign movement on a USD bank",
            Lines = new object[]
            {
                new { AccountId = bankGl.Id, Debit = 100m, Credit = (decimal?)null, Memo = "" },
                new { AccountId = income.Id, Debit = (decimal?)null, Credit = 100m, Memo = "" },
            },
        };
        var refused = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/ledger/journals", manual));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PostingErrorCode.BankCurrencyMismatch.ToString(),
            body.GetProperty("errors")[0].GetProperty("code").GetString());
        var period = await context.FiscalPeriods.SingleAsync(x => x.FiscalYear == 2026 && x.Number == 1);
        var forgedEntry = Guid.NewGuid();
        var forgedLine = Guid.NewGuid();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO gl_entries
                (id, tenant_id, number, journal_code, period_id, posting_date, created_at, memo,
                 source_type, source_id, iteration, status, uses_storno)
            VALUES ({forgedEntry}, {company.World.TenantId.Value}, 'FORGED-1', 'GL', {period.Id},
                {company.World.PostingDate}, now(), '', 'manual.journal', NULL, 1, 'Draft', false)
            """);
        var error = await LedgerHarness.StoreErrorAsync(async () => await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO gl_lines
                (id, tenant_id, entry_id, account_id, line_order, debit, credit, currency,
                 amount_in_currency, exchange_rate, exchange_rate_date, memo)
            VALUES ({forgedLine}, {company.World.TenantId.Value}, {forgedEntry}, {bankGl.Id}, 1, 100, NULL,
                'BDT', 100, 1, {company.World.PostingDate}, '')
            """));
        await transaction.RollbackAsync();
        Assert.Contains("denominated in the registered bank currency", error);
        var bdtGl = new ChartOfAccount
        {
            Id = Guid.NewGuid(), Code = "1024", Name = "BDT bank", Type = AccountType.Asset,
            SubType = AccountSubType.Bank, IsPostable = true, IsActive = true, Path = "bdt_bank",
        };
        context.Accounts.Add(bdtGl);
        await context.SaveChangesAsync();
        _ = await CreateBankAsync(company, bdtGl.Id, "BDTBANK", "BDT");
        var accepted = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/ledger/journals", new
            {
                JournalCode = "GL",
                PostingDate = company.World.PostingDate,
                Memo = "bank charges",
                Lines = new object[]
                {
                    new { AccountId = income.Id, Debit = 100m, Credit = (decimal?)null, Memo = "" },
                    new { AccountId = bdtGl.Id, Debit = (decimal?)null, Credit = 100m, Memo = "" },
                },
            }));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_bank_ids_are_not_visible()
    {
        var harness = new LedgerHarness(factory);
        var first = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        var second = await harness.ProvisionedCompanyWithUserAsync("Accounts");
        var bankGl = await harness.FindAccountByCodeAsync(first.World.TenantId, "1020");
        var bankId = await CreateBankAsync(first, bankGl, "FIRST");
        var response = await second.Client.SendAsync(LedgerHarness.Authenticated(second.Token, HttpMethod.Get,
            $"/api/v1/banking/accounts/{bankId}/reconciliations"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateBankAsync(HarnessCompany company, Guid ledgerId, string code, string currency = "BDT")
    {
        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(company.Token, HttpMethod.Post,
            "/api/v1/banking/accounts", new { LedgerAccountId = ledgerId, Code = code,
                BankName = "Test bank", AccountNumber = code, Currency = currency }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
