namespace Ostagar.Ledger;

/// <summary>
/// Writes a <see cref="PostingRequest"/> into the general ledger, or refuses it.
/// </summary>
/// <remarks>
/// Declared in the ledger and implemented beside the database context, because posting is a ledger
/// concept whose every step is a storage step: locking the journal to take a number, saving a draft,
/// then flipping it to posted. Naming it here keeps the domain free of the persistence dependency that
/// law L18 exists to prevent.
/// </remarks>
public interface IPoster
{
    /// <summary>
    /// Posts one request. Never throws for a rule the request simply fails; the errors come back on the
    /// outcome so a caller can show all of them at once.
    /// </summary>
    Task<PostingOutcome> PostAsync(PostingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects a posted entry by posting its mirror and linking the two.
    /// </summary>
    /// <remarks>
    /// The only way to change what a posted entry says, because the entry itself is immutable
    /// (invariant I3). Mirroring is derived from the original's own lines rather than typed in, so a
    /// correction cannot accidentally be a different amount — the failure mode of a hand-keyed reversing
    /// journal, and the reason a period can look closed while being off by whatever was mistyped.
    /// </remarks>
    Task<PostingOutcome> ReverseAsync(
        Guid entryId,
        DateOnly reversalDate,
        string memo,
        CancellationToken cancellationToken = default);
}
