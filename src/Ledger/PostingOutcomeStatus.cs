namespace Ostagar.Ledger;

/// <summary>
/// How a posting attempt ended.
/// </summary>
/// <remarks>
/// <see cref="AlreadyPosted"/> is a separate answer from <see cref="Posted"/> because they mean
/// different things to the caller: one wrote a new entry, the other found the entry a previous attempt
/// wrote. Collapsing them into a single success is how a retry looks like work that happened twice, and
/// reporting it as a failure is how an idempotent endpoint becomes a 500 on a double click.
/// </remarks>
public enum PostingOutcomeStatus
{
    /// <summary>A new entry was written and posted.</summary>
    Posted,

    /// <summary>The same source revision had already been posted; nothing was written.</summary>
    AlreadyPosted,

    /// <summary>Refused before anything was written.</summary>
    Rejected,
}
