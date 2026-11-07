namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Whether the content of a document may still be changed, decided from its stage rather than from its
/// state name.
/// </summary>
/// <remarks>
/// Separate from the transition rules on purpose. "May I type in this line?" is asked by an edit form and
/// "may I press Approve?" is asked by a button, and the legacy build answered the first question with a
/// client-side hint while the server accepted the write — so an approved purchase order's quantity was
/// quietly editable by anyone who knew the endpoint. The rule stated here is the server-side one, and the
/// only lawful way to change an approved document is the send-back transition that writes a reason.
///
/// <see cref="DocumentLifecycleStage.Posted"/> is deliberately absent alongside <see cref="DocumentLifecycleStage.Approved"/>:
/// a posted document's correction is a reversal, so its content is frozen by a stronger rule than the one
/// that freezes an approved document.
/// </remarks>
public static class DocumentEditPolicy
{
    /// <summary>True only for a stage whose content has not been promised to anyone or is back for amending.</summary>
    public static bool AllowsEdit(DocumentLifecycleStage stage) =>
        stage is DocumentLifecycleStage.Draft or DocumentLifecycleStage.Returned;

    /// <summary>
    /// The refusal to show when editing is not allowed, or null when it is.
    /// </summary>
    /// <remarks>
    /// Returns the code as well as the sentence, because a module that only receives <c>false</c> will
    /// invent its own reason for it, and that invented reason is the second definition of the rule.
    /// </remarks>
    public static WorkflowError? RefusalFor(DocumentLifecycleStage stage) =>
        AllowsEdit(stage)
            ? null
            : new WorkflowError(
                WorkflowErrorCode.DocumentNotEditable,
                stage == DocumentLifecycleStage.Posted
                    ? "A posted document cannot be edited at all; correct it with a reversal document."
                    : "An agreed or submitted document cannot be edited. Send it back with a reason instead, so the change is on the record.");
}
