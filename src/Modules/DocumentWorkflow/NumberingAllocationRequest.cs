namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// A document asking for its number.
/// </summary>
/// <remarks>
/// The key is supplied by the caller rather than derived here, because only the caller knows what makes
/// this request the same request again. The convention that works is
/// <c>DOCUMENT_TYPE:document-id:revision</c>, mirroring the ledger's idempotency key: a retry keeps the
/// revision and gets its number back, a genuinely corrected document raises the revision and consumes a
/// new number.
///
/// <see cref="On"/> is the document date, i.e. the date printed on the paper, not today's date: a
/// back-dated delivery note belongs to the period it says it does, which is the same reasoning that puts
/// the ledger's period lock on the posting date.
///
/// <see cref="ExpectedNextValue"/> is the counter the caller read. Null skips the optimistic check; a
/// mismatch refuses rather than allocates, so a lost race cannot emit two numbers for one document.
/// </remarks>
public sealed record NumberingAllocationRequest
{
    /// <summary>Identity of this allocation. Required, and never reused by two documents.</summary>
    public required string AllocationKey { get; init; }

    /// <summary>The document date the number is issued for.</summary>
    public required DateOnly On { get; init; }

    /// <summary>The counter value the caller believes it is allocating against.</summary>
    public int? ExpectedNextValue { get; init; }
}
