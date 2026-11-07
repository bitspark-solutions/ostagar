using Ostagar.Ledger;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves the only permitted way to change what the ledger has already said: post the mirror and link
/// the two, so the mistake stays visible.
/// </summary>
/// <remarks>
/// The legacy build corrected entries by editing them. Nothing recorded what the entry had originally
/// said, so a figure that had been changed twice was indistinguishable from one that had never been
/// touched — and a trial balance that balanced proved nothing about whether any of it was true.
///
/// The tests below therefore assert on <b>what is left visible</b> as much as on the arithmetic. A
/// reversal that silently replaced the original would produce the same net totals as a correct one.
/// </remarks>
public sealed class ReversalTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly LedgerHarness ledger = new(factory);

    [Fact]
    public async Task Reversing_a_posted_entry_adds_a_linked_entry_and_leaves_the_original_alone()
    {
        var world = await ledger.ProvisionAsync();

        var original = await ledger.PostAsync(world, amount: 500m);
        var reversal = await ledger.ReverseAsync(world, original.EntryId!.Value);

        Assert.Equal(PostingOutcomeStatus.Posted, reversal.Status);
        Assert.Equal("GL-000002", reversal.Number);

        var ids = await ledger.EntryIdsAsync(world);
        Assert.Equal(2, ids.Count);

        // Both entries still exist. The count is the whole point: a correction implemented as an update
        // would report a net zero ledger and one entry, and every assertion about amounts would still pass.
        Assert.Contains(original.EntryId!.Value, ids);
        Assert.Contains(reversal.EntryId!.Value, ids);
    }

    [Fact]
    public async Task A_reversal_leaves_every_account_it_touched_at_zero()
    {
        var world = await ledger.ProvisionAsync();

        var original = await ledger.PostAsync(world, amount: 500m);
        await ledger.ReverseAsync(world, original.EntryId!.Value);

        var net = await ledger.NetByAccountAsync(world);

        Assert.Equal(0m, net[world.Cash]);
        Assert.Equal(0m, net[world.Income]);
    }

    [Fact]
    public async Task Reversing_the_same_entry_twice_is_refused()
    {
        var world = await ledger.ProvisionAsync();

        var original = await ledger.PostAsync(world, amount: 500m);

        await ledger.ReverseAsync(world, original.EntryId!.Value);
        var second = await ledger.ReverseAsync(world, original.EntryId!.Value);

        // Two clicks on one button must not become two corrections.
        Assert.Equal(PostingOutcomeStatus.Rejected, second.Status);
        Assert.Equal(PostingErrorCode.AlreadyReversed, second.Errors[0].Code);
        Assert.Equal(2, (await ledger.EntryIdsAsync(world)).Count);
    }

    [Fact]
    public async Task Reversing_a_draft_is_refused_because_a_draft_is_edited_not_reversed()
    {
        var world = await ledger.ProvisionAsync();

        await ledger.WriteRawAsync(world, entry =>
        {
            entry.Lines.Add(LedgerHarness.Line(world.Cash, debit: 500m));
            entry.Lines.Add(LedgerHarness.Line(world.Income, credit: 500m));
        }, post: false);

        var draftId = (await ledger.EntryIdsAsync(world))[0];

        var outcome = await ledger.ReverseAsync(world, draftId);

        Assert.Equal(PostingErrorCode.EntryNotPosted, outcome.Errors[0].Code);
        Assert.Contains("draft", outcome.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reversing_an_unknown_entry_does_not_confirm_it_exists_elsewhere()
    {
        var world = await ledger.ProvisionAsync();

        var outcome = await ledger.ReverseAsync(world, Guid.NewGuid());

        Assert.Equal(PostingErrorCode.EntryNotFound, outcome.Errors[0].Code);
        Assert.DoesNotContain("another", outcome.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_retry_of_the_same_reversal_is_one_entry_not_two()
    {
        var world = await ledger.ProvisionAsync();

        var original = await ledger.PostAsync(world, amount: 500m);
        var entryId = original.EntryId!.Value;

        var first = await ledger.ReverseAsync(world, entryId);
        var retry = await ledger.ReverseAsync(world, entryId);

        // Reached by the already-reversed rule rather than the idempotency index, because the original
        // entry is what the caller keys on — but the answer must still be "nothing new happened".
        Assert.Equal(PostingOutcomeStatus.Rejected, retry.Status);
        Assert.Equal(2, (await ledger.EntryIdsAsync(world)).Count);
        Assert.NotNull(first.EntryId);
    }

    [Fact]
    public async Task A_reversal_into_a_locked_period_is_refused_like_any_other_posting()
    {
        var world = await ledger.ProvisionAsync();

        var original = await ledger.PostAsync(world, amount: 500m);

        await ledger.LockPeriodAsync(world);

        var outcome = await ledger.ReverseAsync(world, original.EntryId!.Value);

        // Correcting a mistake is not a reason to reopen a closed year. The answer is to reverse into the
        // period that is currently open, which the caller chooses by passing a later date.
        Assert.Equal(PostingErrorCode.PeriodLocked, outcome.Errors[0].Code);
    }

    [Fact]
    public async Task A_reversal_of_a_foreign_entry_reads_as_absent()
    {
        var mine = await ledger.ProvisionAsync();
        var theirs = await ledger.ProvisionAsync();

        var theirEntry = await ledger.PostAsync(theirs, amount: 700m);

        var outcome = await ledger.ReverseAsync(mine, theirEntry.EntryId!.Value);

        Assert.Equal(PostingErrorCode.EntryNotFound, outcome.Errors[0].Code);
    }
}
