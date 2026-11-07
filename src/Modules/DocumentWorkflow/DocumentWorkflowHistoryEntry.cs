using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One decision about one document, recorded forever: who, when, from where, to where, and why.
/// </summary>
/// <remarks>
/// A record with <c>init</c> properties, so "append-only history" is a property of the type rather than a
/// promise in a service: there is nothing to update, and a store that wanted to rewrite an entry would
/// have to construct a second row that pretends to be the first. Reversing a decision therefore adds
/// another entry with <see cref="CorrectsEntryId"/> pointing at the original, which keeps both visible —
/// the workflow version of the ledger's rule that a posted entry is corrected by a reversal, never by an
/// edit. A history that could be rewritten would leave an approver free to change their mind
/// retroactively, which is exactly the thing a signed approval is supposed to make impossible.
///
/// <see cref="Id"/> is empty until the store writes the row. The engine that decides the transition is
/// not the component that allocates identifiers, and an id minted here would be another cash-book
/// controller reporting a saved journal that no one saved.
/// </remarks>
public sealed record DocumentWorkflowHistoryEntry : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public required DocumentTypeCode DocumentType { get; init; }

    /// <summary>The document this decision is about.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Position in this document's trail, one-based and never reused.</summary>
    public int EntryNumber { get; init; }

    public required string FromState { get; init; }

    public required string ToState { get; init; }

    /// <summary>Who made the decision. Never inferred from an ambient request.</summary>
    public required Guid ActorUserId { get; init; }

    /// <summary>When the decision was made, as stated by the caller.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Why, when the transition demanded one; otherwise may be empty.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// The entry this decision reverses, or null when it is an ordinary move.
    /// </summary>
    /// <remarks>
    /// A link rather than a rewrite, for the same reason the ledger reverses instead of editing: an
    /// auditor has to see both the original decision and the change of position, and the second one
    /// silently replacing the first is the difference between a trail and a story.
    /// </remarks>
    public Guid? CorrectsEntryId { get; init; }
}
