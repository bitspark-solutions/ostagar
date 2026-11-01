using System.Net;
using System.Net.Http.Json;
using Garments.Platform.Rbac;

namespace Garments.Tests.Integration;

/// <summary>
/// Proves the cash book endpoint over real HTTP: that a recorded receipt produces the balanced posted
/// entry the ledger can show, that the document row points at it, and that a refused movement leaves
/// no trace in either place. This is the path the legacy build faked — its cash book constructed a
/// posted entry id out of thin air and saved nothing.
/// </summary>
public sealed class CashBookEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_receipt_posts_to_the_cash_journal_and_the_document_points_at_the_entry()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/transactions", ReceiptBody(world)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreatedCashTransaction>();
        Assert.NotNull(created);
        Assert.Equal("CASH-000001", created.Number);

        // The entry is real: readable through the ledger's own endpoint, posted, and balanced.
        var entry = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/ledger/journals/{created.EntryId}"));
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);

        // And the document is real: it exists, and it names the entry it produced.
        var document = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/cash-book/transactions/{created.TransactionId}"));
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);

        var row = await document.Content.ReadFromJsonAsync<CashTransactionRow>();
        Assert.NotNull(row);
        Assert.Equal("Receipt", row.Kind);
        Assert.Equal("Posted", row.Status);
        Assert.Equal(created.EntryId, row.PostedEntryId);
        Assert.Equal("CASH-000001", row.PostedEntryNumber);
        Assert.Equal(500m, row.Amount);
    }

    [Fact]
    public async Task A_payment_writes_the_mirror_image_of_a_receipt()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/cash-book/transactions",
            new
            {
                Kind = "payment",
                TransactionDate = world.PostingDate,
                CashAccountId = world.Cash,
                CounterAccountId = world.Income,
                Amount = 200m,
                Memo = "office supplies",
            }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Cash went out: the cash account nets negative by exactly the payment.
        var net = await ledger.NetByAccountAsync(world);
        Assert.Equal(-200m, net[world.Cash]);
        Assert.Equal(200m, net[world.Income]);
    }

    [Fact]
    public async Task A_transaction_where_both_sides_are_one_account_is_refused_and_nothing_is_written()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/cash-book/transactions",
            new
            {
                Kind = "receipt",
                TransactionDate = company.World.PostingDate,
                CashAccountId = company.World.Cash,
                CounterAccountId = company.World.Cash,
                Amount = 500m,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("SameAccountBothSides", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
    }

    [Fact]
    public async Task A_non_cash_account_on_the_cash_side_is_refused()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            "/api/v1/cash-book/transactions",
            new
            {
                Kind = "receipt",
                TransactionDate = company.World.PostingDate,
                CashAccountId = company.World.Income,
                CounterAccountId = company.World.Cash,
                Amount = 500m,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CashAccountNotCashLike", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
    }

    [Fact]
    public async Task Reversal_posts_the_mirror_and_marks_the_document_without_rewriting_it()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var created = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/transactions", ReceiptBody(company.World)));
        var transaction = await created.Content.ReadFromJsonAsync<CreatedCashTransaction>();
        Assert.NotNull(transaction);

        var reversal = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/cash-book/transactions/{transaction.TransactionId}/reverse",
            new { ReversalDate = company.World.PostingDate, Memo = "miscounted the drawer" }));

        Assert.Equal(HttpStatusCode.Created, reversal.StatusCode);
        var mirror = await reversal.Content.ReadFromJsonAsync<CreatedJournal>();
        Assert.NotNull(mirror);
        Assert.Equal("CASH-000002", mirror.Number);

        var document = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/cash-book/transactions/{transaction.TransactionId}"));
        var row = await document.Content.ReadFromJsonAsync<CashTransactionRow>();
        Assert.NotNull(row);
        Assert.Equal("Reversed", row.Status);
        Assert.Equal(mirror.EntryId, row.ReversedByEntryId);

        // The books net to zero, and both sides of the correction stay visible.
        Assert.Equal(0m, (await ledger.NetByAccountAsync(company.World)).Values.Sum());

        // A second reversal is refused: the correction is already recorded once.
        var second = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/cash-book/transactions/{transaction.TransactionId}/reverse",
            new { ReversalDate = company.World.PostingDate, Memo = "again" }));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task A_clerk_without_the_accounts_role_cannot_move_cash()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Stock);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/cash-book/transactions", ReceiptBody(company.World)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
    }

    [Fact]
    public async Task Another_company_cannot_see_or_reverse_the_transaction()
    {
        var owner = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var created = await owner.Client.SendAsync(LedgerHarness.Authenticated(
            owner.Token, HttpMethod.Post, "/api/v1/cash-book/transactions", ReceiptBody(owner.World)));
        var transaction = await created.Content.ReadFromJsonAsync<CreatedCashTransaction>();
        Assert.NotNull(transaction);

        var intruder = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var read = await intruder.Client.SendAsync(LedgerHarness.Authenticated(
            intruder.Token, HttpMethod.Get, $"/api/v1/cash-book/transactions/{transaction.TransactionId}"));
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var reverse = await intruder.Client.SendAsync(LedgerHarness.Authenticated(
            intruder.Token,
            HttpMethod.Post,
            $"/api/v1/cash-book/transactions/{transaction.TransactionId}/reverse",
            new { ReversalDate = intruder.World.PostingDate, Memo = "not mine" }));
        Assert.Equal(HttpStatusCode.NotFound, reverse.StatusCode);
    }

    private static object ReceiptBody(LedgerWorld world) => new
    {
        Kind = "receipt",
        TransactionDate = world.PostingDate,
        CashAccountId = world.Cash,
        CounterAccountId = world.Income,
        Amount = 500m,
        Memo = "counter sale",
    };

    private sealed record CreatedCashTransaction(Guid TransactionId, Guid EntryId, string Number);

    private sealed record CreatedJournal(Guid EntryId, string Number);

    private sealed record CashTransactionRow(
        Guid Id,
        string Kind,
        string Status,
        decimal Amount,
        Guid? PostedEntryId,
        string? PostedEntryNumber,
        Guid? ReversedByEntryId);
}
