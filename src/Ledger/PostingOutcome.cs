namespace Ostagar.Ledger;

/// <summary>
/// The result of a posting attempt, including the entry it wrote or found.
/// </summary>
/// <remarks>
/// <see cref="EntryId"/> is absent on a rejection and never a placeholder. The legacy tree returned
/// <c>Guid.NewGuid()</c> as a created identifier from a write that had not persisted anything, so the
/// caller held an id that matched no row and nothing downstream could tell the difference.
/// </remarks>
/// <param name="Status">How the attempt ended.</param>
/// <param name="Errors">Every rule that refused the posting. Empty unless <paramref name="Status"/> is
/// <see cref="PostingOutcomeStatus.Rejected"/>.</param>
/// <param name="EntryId">The entry written, or the one already there.</param>
/// <param name="Number">The journal number assigned, for the caller to show the user what they filed.</param>
public sealed record PostingOutcome(
    PostingOutcomeStatus Status,
    IReadOnlyList<PostingError> Errors,
    Guid? EntryId = null,
    string? Number = null)
{
    public bool Succeeded => Status != PostingOutcomeStatus.Rejected;
}
