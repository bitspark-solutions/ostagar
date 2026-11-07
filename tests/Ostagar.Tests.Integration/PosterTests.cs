using Ostagar.Ledger;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the posting engine does the three things a ledger's front door has to do: assign numbers with
/// no gaps, refuse without consuming one, and post a given document revision exactly once.
/// </summary>
/// <remarks>
/// These are the properties the legacy build had no name for. Its cash book took identifiers from
/// <c>Guid.NewGuid()</c> at the API boundary and its numbering was a per-year count computed on read, so
/// a deleted entry silently renumbered every later one and a retried payment posted twice.
///
/// <see cref="LedgerInvariantTests"/> covers what the schema refuses; this covers what the engine must
/// still get right on top of it. Both layers are tested because each can be correct while the other is
/// not — and only one of them produces a journal number.
/// </remarks>
public sealed class PosterTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    private static readonly string[] FirstThreeJournalNumbers = ["GL-000001", "GL-000002", "GL-000003"];

    [Fact]
    public async Task A_balanced_request_posts_and_carries_the_journals_first_number()
    {
        var world = await ledger.ProvisionAsync();

        var outcome = await ledger.PostAsync(world);

        Assert.Equal(PostingOutcomeStatus.Posted, outcome.Status);
        Assert.NotNull(outcome.EntryId);
        Assert.Equal("GL-000001", outcome.Number);
    }

    [Fact]
    public async Task Consecutive_postings_number_with_no_gap()
    {
        var world = await ledger.ProvisionAsync();

        var first = await ledger.PostAsync(world, amount: 100m);
        var second = await ledger.PostAsync(world, amount: 200m);
        var third = await ledger.PostAsync(world, amount: 300m);

        string?[] issued = [first.Number, second.Number, third.Number];

        Assert.Equal(
            FirstThreeJournalNumbers,
            issued.Select(number => number!).ToArray());

        Assert.Equal(
            ["GL-000001", "GL-000002", "GL-000003"],
            await ledger.NumbersAsync(world));
    }

    /// <summary>
    /// The reason validation runs before the number is taken, and the reason the allocation shares a
    /// transaction with the posting it belongs to.
    /// </summary>
    [Fact]
    public async Task A_refused_request_does_not_consume_a_journal_number()
    {
        var world = await ledger.ProvisionAsync();

        var refused = await ledger.PostAsync(world, new PostingRequest
        {
            ActorUserId = LedgerHarness.Actor,
            JournalCode = "GL",
            PostingDate = world.PostingDate,
            SourceType = "test.manual",
            Lines = [LedgerHarness.Debit(world.Cash, 500m), LedgerHarness.Credit(world.Income, 499m)],
        });

        Assert.Equal(PostingOutcomeStatus.Rejected, refused.Status);

        var accepted = await ledger.PostAsync(world);

        Assert.Equal("GL-000001", accepted.Number);
    }

    [Fact]
    public async Task The_same_source_revision_posts_once_and_the_retry_reports_the_original()
    {
        var world = await ledger.ProvisionAsync();
        var sourceId = Guid.NewGuid();

        var first = await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId);
        var retry = await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId);

        Assert.Equal(PostingOutcomeStatus.Posted, first.Status);
        Assert.Equal(PostingOutcomeStatus.AlreadyPosted, retry.Status);
        Assert.Equal(first.EntryId, retry.EntryId);
        Assert.Equal(first.Number, retry.Number);

        // The assertion that matters: a retry that reports itself as successful and leaves one entry is
        // only honest if there really is one entry.
        Assert.Single(await ledger.NumbersAsync(world));
    }

    [Fact]
    public async Task A_corrected_document_at_the_next_revision_is_a_new_posting()
    {
        var world = await ledger.ProvisionAsync();
        var sourceId = Guid.NewGuid();

        var original = await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId, iteration: 1);
        var corrected = await ledger.PostAsync(world, sourceType: "sales.invoice", sourceId: sourceId, iteration: 2);

        Assert.Equal(PostingOutcomeStatus.Posted, original.Status);
        Assert.Equal(PostingOutcomeStatus.Posted, corrected.Status);
        Assert.NotEqual(original.EntryId, corrected.EntryId);
        Assert.Equal(2, (await ledger.NumbersAsync(world)).Count);
    }

    [Fact]
    public async Task A_manual_journal_without_a_source_is_not_treated_as_a_retry()
    {
        var world = await ledger.ProvisionAsync();

        var first = await ledger.PostAsync(world, sourceType: "manual", sourceId: null);
        var second = await ledger.PostAsync(world, sourceType: "manual", sourceId: null);

        // Idempotency keyed on a null source id would collapse every manual journal in the company into
        // one entry, and the second clerk would be told their posting had already happened.
        Assert.Equal(PostingOutcomeStatus.Posted, first.Status);
        Assert.Equal(PostingOutcomeStatus.Posted, second.Status);
    }

    [Fact]
    public async Task Posting_at_a_parent_account_is_refused_with_that_account_named()
    {
        var world = await ledger.ProvisionAsync();
        var parentId = await ledger.AddParentAccountAsync(world);

        var outcome = await ledger.PostAsync(world, debitAccountId: parentId);

        Assert.Equal(PostingOutcomeStatus.Rejected, outcome.Status);
        Assert.Equal(PostingErrorCode.AccountUnpostable, outcome.Errors[0].Code);
        Assert.Contains("100 Current Assets", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_guessed_account_id_from_another_company_reads_as_absent_not_foreign()
    {
        var world = await ledger.ProvisionAsync();
        var foreignAccountId = await ledger.ForeignAccountIdAsync();

        var outcome = await ledger.PostAsync(world, debitAccountId: foreignAccountId);

        // The message must not confirm the id exists somewhere else. Whether the tenant isolation works is
        // <see cref="TenantIsolationTests"/>'s question; this is the narrower one about what a refusal
        // leaks while it is being helpful.
        Assert.Equal(PostingErrorCode.AccountUnpostable, outcome.Errors[0].Code);
        Assert.DoesNotContain("another", outcome.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not have", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Posting_into_an_unprovisioned_period_is_refused_before_any_number_is_taken()
    {
        var world = await ledger.ProvisionAsync();

        var outcome = await ledger.PostAsync(world, postingDate: new DateOnly(2030, 1, 1));

        Assert.Equal(PostingErrorCode.PeriodNotFound, outcome.Errors[0].Code);
        Assert.Null(outcome.Number);
        Assert.Empty(await ledger.NumbersAsync(world));
    }

    [Fact]
    public async Task Posting_into_a_locked_period_names_the_lock_instead_of_the_schema()
    {
        var world = await ledger.ProvisionAsync();

        await ledger.LockPeriodAsync(world);

        var outcome = await ledger.PostAsync(world);

        Assert.Equal(PostingErrorCode.PeriodLocked, outcome.Errors[0].Code);

        // The harness locks through the period's own end date, which is 31 July — the last day it will
        // accept a posting on, and the reason the identical request succeeds in every other test here.
        Assert.Contains("locked through 2026-07-31", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_journal_code_is_refused_without_an_exception_from_the_database()
    {
        var world = await ledger.ProvisionAsync();

        var outcome = await ledger.PostAsync(world, new PostingRequest
        {
            ActorUserId = LedgerHarness.Actor,
            JournalCode = "MISSING",
            PostingDate = world.PostingDate,
            SourceType = "test.manual",
            Lines = [LedgerHarness.Debit(world.Cash, 500m), LedgerHarness.Credit(world.Income, 500m)],
        });

        Assert.Equal(PostingErrorCode.JournalNotFound, outcome.Errors[0].Code);
        Assert.Contains("MISSING", outcome.Errors[0].Message, StringComparison.Ordinal);
    }
}
