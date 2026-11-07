namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One move a document is allowed to make, and the conditions attached to it.
/// </summary>
/// <remarks>
/// Declared data, not code. A transition the machine cannot express is a transition nobody can add
/// without editing the engine, which is how a rule ends up existing in one module and not in the other
/// four; a document type that needs a new path adds rows to its
/// <see cref="DocumentWorkflowDefinition"/> and the rules come along unchanged.
/// </remarks>
/// <param name="From">State the document must be in for this move to be offered.</param>
/// <param name="To">State it lands in.</param>
/// <param name="Actor">Who may perform it.</param>
/// <param name="RequiresReason">Whether a written justification is demanded. Send-backs, rejections and
/// cancellations always do: those are the three moves a later reader needs an explanation for, and a
/// history that records the decision but not the reason cannot be used to review it.</param>
public sealed record DocumentTransition(
    string From,
    string To,
    WorkflowActorRequirement Actor = WorkflowActorRequirement.None,
    bool RequiresReason = false);
