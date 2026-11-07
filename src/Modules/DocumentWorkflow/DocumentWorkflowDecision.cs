namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// What the machine decided about a requested transition.
/// </summary>
/// <remarks>
/// <see cref="EntryToAppend"/> travels with an accepted decision rather than the decision writing it,
/// because the state change and the history row must be one transaction: a document whose approval moved
/// without its history entry being written has a decision nobody can prove, and a history entry written
/// against a document that did not move is a fiction. Neither half may be persisted without the other, so
/// neither half is done here.
/// </remarks>
/// <param name="Status">Accepted or refused.</param>
/// <param name="Errors">Every rule that refused, so a screen can show all of them at once.</param>
/// <param name="NewState">The state the document moves to. Null on a refusal.</param>
/// <param name="EntryToAppend">The history row to write. Null on a refusal, and its
/// <see cref="DocumentWorkflowHistoryEntry.Id"/> is deliberately empty until the store assigns one.</param>
public sealed record DocumentWorkflowDecision(
    WorkflowDecisionStatus Status,
    IReadOnlyList<WorkflowError> Errors,
    DocumentStateDefinition? NewState = null,
    DocumentWorkflowHistoryEntry? EntryToAppend = null)
{
    public bool Accepted => Status == WorkflowDecisionStatus.Accepted;
}
