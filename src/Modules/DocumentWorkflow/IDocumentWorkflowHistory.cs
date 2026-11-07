using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The store for document decisions, and deliberately only a reader and an appender.
/// </summary>
/// <remarks>
/// There is no update or delete method on this interface, and that absence is the append-only rule
/// expressed in the shape a module can call. A history that can be rewritten is not a record of what was
/// decided but of what somebody currently wants it to look like, and correcting a decision by editing it
/// destroys the one thing the trail exists to keep: that a document was approved by a named person on a
/// named day, and that the position later changed. The lawful way to change a recorded decision is
/// another entry with <see cref="DocumentWorkflowHistoryEntry.CorrectsEntryId"/> set.
///
/// Implemented beside the database context by the integrator, like the ledger's
/// <see cref="Ostagar.Ledger.IPoster"/>: assigning the row id, ordering by
/// <see cref="DocumentWorkflowHistoryEntry.EntryNumber"/> per document, and enforcing that ordering with a
/// unique index are storage jobs, not domain ones.
/// </remarks>
public interface IDocumentWorkflowHistory
{
    /// <summary>Every decision recorded for one document, oldest first.</summary>
    Task<IReadOnlyList<DocumentWorkflowHistoryEntry>> LoadAsync(
        TenantId tenantId,
        DocumentTypeCode documentType,
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one decision. The implementation assigns <see cref="DocumentWorkflowHistoryEntry.Id"/> and
    /// returns the persisted entry; a caller that needs the document's state and this row to commit
    /// together must do both in the same transaction.
    /// </summary>
    Task<DocumentWorkflowHistoryEntry> AppendAsync(
        DocumentWorkflowHistoryEntry entry,
        CancellationToken cancellationToken = default);
}
