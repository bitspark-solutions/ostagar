using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The document as the machine sees it: who owns it, who made it, where it stands, and what has already
/// been decided about it.
/// </summary>
/// <remarks>
/// <see cref="History"/> is not a convenience. Rules like "the creator may not approve" and "this decision
/// is a correction of that one" are only evaluable from the record of decisions, and passing the record in
/// explicitly is what lets the engine be pure and tested without a database. It also makes the document's
/// claimed state checkable: <see cref="WorkflowErrorCode.HistoryDoesNotMatchState"/> fires when the state
/// column and the trail disagree, which is how a hand-edited row becomes visible instead of merely
/// suspicious.
///
/// <see cref="CreatorActorId"/> is required rather than nullable. A document with no named creator is a
/// document nobody can be stopped from approving, which is the exact hole the legacy
/// <c>Guid.Parse("00000000-…")</c> actor placeholder opened.
///
/// <see cref="TenantId"/> is declared by the caller rather than read from an ambient request context, so a
/// background job cannot decide a document belonging to the tenant that context happens to be holding.
/// </remarks>
public sealed record DocumentWorkflowSubject
{
    /// <summary>The tenant that owns the document.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Which definition governs it.</summary>
    public required DocumentTypeCode DocumentType { get; init; }

    /// <summary>The row the workflow history is filed against.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Where the document claims to be.</summary>
    public required string CurrentStateCode { get; init; }

    /// <summary>Who made it, and therefore who may not approve it.</summary>
    public required Guid CreatorActorId { get; init; }

    /// <summary>Every decision already recorded, oldest first.</summary>
    public IReadOnlyList<DocumentWorkflowHistoryEntry> History { get; init; } = [];
}
