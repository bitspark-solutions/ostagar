using System.Net;
using System.Net.Http.Json;
using Ostagar.Ledger;
using System.Globalization;
using Ostagar.Platform.Rbac;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the ledger endpoints over real HTTP: that a 201 means a row exists, that a refusal writes
/// nothing, and that posting a journal is a permission a clerk's role actually holds.
/// </summary>
/// <remarks>
/// The legacy build's accounting controllers were reachable but threw on activation, because 22 of their
/// service registrations sat commented out and nothing ever asked whether the process came up. These tests
/// read back through <see cref="IPoster"/>'s own store after every write, which is the question law L3
/// refuses to let a passing test answer with a status code alone.
/// </remarks>
public sealed class LedgerJournalEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task A_posted_journal_returns_a_number_and_the_entry_is_really_there()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        var world = company.World;

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(world)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreatedJournal>();
        Assert.NotNull(created);
        Assert.Equal("GL-000001", created.Number);

        // The claim that matters: the id the API returned resolves to the entry it said it wrote.
        var read = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/ledger/journals/{created.EntryId}"));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var entry = await read.Content.ReadFromJsonAsync<JournalEntry>();
        Assert.NotNull(entry);
        Assert.Equal(created.EntryId, entry.Id);
        Assert.Equal("Posted", entry.Status);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(entry.TotalDebit, entry.TotalCredit);

        Assert.Contains(created.Number, await ledger.EntryNumbersAsync(world));
    }

    [Fact]
    public async Task An_unbalanced_journal_is_refused_and_no_entry_is_written()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", UnbalancedBody(company.World)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unbalanced", body, StringComparison.Ordinal);

        // Nothing half-written, and no journal number consumed: a refusal that took a number would open
        // the gap invariant I4 promises the journal will not have.
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
        Assert.Equal(1, await ledger.JournalNextNumberAsync(company.World));
    }

    [Fact]
    public async Task A_clerk_whose_role_lacks_the_journal_keys_cannot_post()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Stock);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(company.World)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
    }

    [Fact]
    public async Task A_signed_in_user_may_read_the_ledger_without_being_able_to_change_it()
    {
        var writer = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);
        await writer.Client.SendAsync(LedgerHarness.Authenticated(
            writer.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(writer.World)));

        // The read path is scoped to the company that wrote it; a different clerk in the same company with
        // no write rights must still be able to see the entry, because a ledger nobody can read is not a
        // ledger an owner will trust.
        var reader = await ledger.AddClerkAsync(writer.World, DefaultRoleMatrix.Manager);

        var response = await reader.Client.SendAsync(LedgerHarness.Authenticated(
            reader.Token, HttpMethod.Get, "/api/v1/ledger/journals"));

        // periodId is required by design, so an absent filter is a 400 rather than an unbounded scan.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var forbidden = await reader.Client.SendAsync(LedgerHarness.Authenticated(
            reader.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(writer.World)));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Another_company_cannot_open_a_journal_by_guessing_its_id()
    {
        var owner = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var created = await owner.Client.SendAsync(LedgerHarness.Authenticated(
            owner.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(owner.World)));

        var entry = await created.Content.ReadFromJsonAsync<CreatedJournal>();
        Assert.NotNull(entry);

        var intruder = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var response = await intruder.Client.SendAsync(LedgerHarness.Authenticated(
            intruder.Token, HttpMethod.Get, $"/api/v1/ledger/journals/{entry.EntryId}"));

        // Absent, not forbidden: the tenant filter makes the row invisible, and a 403 here would confirm
        // that the guessed id belongs to somebody, which is an enumeration primitive.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reversal_posts_a_mirror_and_leaves_the_original_exactly_as_it_was()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        var created = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(company.World)));

        var entry = await created.Content.ReadFromJsonAsync<CreatedJournal>();
        Assert.NotNull(entry);

        var reversal = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token,
            HttpMethod.Post,
            $"/api/v1/ledger/journals/{entry.EntryId}/reverse",
            new { ReversalDate = new DateOnly(2026, 7, 1), Memo = "keyed the wrong amount" }));

        Assert.Equal(HttpStatusCode.Created, reversal.StatusCode);

        var reversed = await reversal.Content.ReadFromJsonAsync<CreatedJournal>();
        Assert.NotNull(reversed);

        var original = await ReadEntryAsync(company, entry.EntryId);
        var mirror = await ReadEntryAsync(company, reversed.EntryId);

        Assert.NotNull(original);
        Assert.NotNull(mirror);

        Assert.Equal("GL-000001", original.Number);
        Assert.Equal("GL-000002", mirror.Number);
        Assert.Equal(entry.EntryId, mirror.ReversesEntryId);
        // The mirror's id, not the original's: this field names the entry that corrected this one, which
        // is what lets a reader see both sides from the original alone. Asserting the original's own id
        // here would have been the test contradicting the field's documented meaning.
        Assert.Equal(reversed.EntryId, original.ReversedByEntryId);

        // Both sides of the correction visible, and the original's lines unchanged — which is the whole
        // difference between a reversal and the edit that used to destroy the evidence.
        Assert.Equal(500m, original.TotalDebit);
        Assert.Equal(500m, mirror.TotalDebit);
        Assert.Equal(0m, (await ledger.NetByAccountAsync(company.World)).Values.Sum());
    }

    [Fact]
    public async Task A_journal_into_a_locked_period_is_refused_with_the_ledger_reason_not_a_server_error()
    {
        var company = await ledger.ProvisionedCompanyWithUserAsync(DefaultRoleMatrix.Accounts);

        await ledger.LockPeriodAsync(company.World);

        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Post, "/api/v1/ledger/journals", BalancedBody(company.World)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        // The database's own sentence, passed through. A 500 here would be the honest-looking version of
        // the worst outcome: the clerk would retry, and the ledger would still be closed.
        Assert.Contains("PeriodLocked", body, StringComparison.Ordinal);
        Assert.Empty(await ledger.EntryIdsAsync(company.World));
    }

    private static object BalancedBody(LedgerWorld world) => new
    {
        JournalCode = "GL",
        PostingDate = new DateOnly(2026, 7, 1),
        Memo = "typed by the endpoint suite",
        Lines = new object[]
        {
            new { AccountId = world.Cash, Debit = 500m, Credit = (decimal?)null, Memo = "cash in" },
            new { AccountId = world.Income, Debit = (decimal?)null, Credit = 500m, Memo = "revenue" },
        },
    };

    private static object UnbalancedBody(LedgerWorld world) => new
    {
        JournalCode = "GL",
        PostingDate = new DateOnly(2026, 7, 1),
        Memo = "one paisa short",
        Lines = new object[]
        {
            new { AccountId = world.Cash, Debit = 500m, Credit = (decimal?)null, Memo = "cash in" },
            new { AccountId = world.Income, Debit = (decimal?)null, Credit = 499.99m, Memo = "revenue" },
        },
    };


    private static async Task<JournalEntry?> ReadEntryAsync(
        HarnessCompany company,
        Guid entryId)
    {
        var response = await company.Client.SendAsync(LedgerHarness.Authenticated(
            company.Token, HttpMethod.Get, $"/api/v1/ledger/journals/{entryId}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JournalEntry>();
    }


    private sealed record CreatedJournal(Guid EntryId, string Number);

    private sealed record JournalLine(Guid Id, Guid AccountId, string AccountCode, decimal? Debit, decimal? Credit);

    private sealed record JournalEntry(
        Guid Id,
        string Number,
        string Status,
        Guid? ReversesEntryId,
        Guid? ReversedByEntryId,
        decimal TotalDebit,
        decimal TotalCredit,
        List<JournalLine> Lines);
}
