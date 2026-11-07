namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// What a document's state means for what may still be done to it, independent of what the state is
/// called.
/// </summary>
/// <remarks>
/// The rules read the stage, never a state name. That indirection is the whole point of doing workflows
/// here instead of in each module: a purchasing clerk's <c>"APPROVED"</c> and a sales clerk's
/// <c>"FINAL"</c> can both be <see cref="Approved"/>, so "nobody edits an approved document" is written
/// once and cannot be silently weaker in one module than another. The legacy tree expressed the same
/// idea as string comparisons scattered through services — <c>status == "Approved"</c> in one place,
/// <c>status == "FINALISED"</c> in another — and the three screens beside each other gave three answers
/// from one database.
/// </remarks>
public enum DocumentLifecycleStage
{
    /// <summary>Free to edit. Nothing has been promised to anyone yet.</summary>
    Draft,

    /// <summary>Submitted for somebody else's decision. Still not editable, because it has been shown.</summary>
    PendingApproval,

    /// <summary>Agreed. Content is frozen; only a send-back reopens it.</summary>
    Approved,

    /// <summary>
    /// Recorded in the ledger and terminal. Its only correction is a reversal document, mirroring ledger
    /// invariant I3 — a workflow that lets a posted document be edited would make the ledger's immutability
    /// a lie, so the workflow must never be the weaker of the two rules.
    /// </summary>
    Posted,

    /// <summary>Sent back to be amended. Editable again, with a reason on record.</summary>
    Returned,

    /// <summary>Refused. Whether the document can be revived is a decision per document type, not a default.</summary>
    Rejected,

    /// <summary>Withdrawn. Consumes the number it was given and stops existing commercially.</summary>
    Cancelled,
}
