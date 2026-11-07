namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// How a workflow decision ended.
/// </summary>
public enum WorkflowDecisionStatus
{
    /// <summary>The move is allowed: a new state, and a history entry that must be written with it.</summary>
    Accepted,

    /// <summary>The move is refused, and the document keeps its state and its history.</summary>
    Refused,
}
