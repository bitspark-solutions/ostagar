using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Evaluates a requested document transition against a declared workflow definition.
/// </summary>
/// <remarks>
/// The single place any module asks "may this document move", so the answer is the same on the screen,
/// in the batch job and in the test. Rules are read off the definition
/// (<see cref="DocumentTransition.Actor"/>, <see cref="DocumentTransition.RequiresReason"/>,
/// <see cref="DocumentStateDefinition.IsTerminal"/>) rather than written where they are used, which is how
/// one document type's rule stops being weaker than another's.
///
/// It refuses, and nothing else: it does not save the document, does not touch the ledger and does not
/// mint ids. The integrator's job is to take an accepted decision and write the new state and
/// <see cref="DocumentWorkflowDecision.EntryToAppend"/> in the same transaction — the two halves of a
/// decision that cannot be separated, because a state change without its history entry is an approval
/// nobody can prove.
///
/// Every rule below can be traced to the failure it prevents: an approval by the creator (segregation of
/// duties on paper), an edit after approval (a document that changed after being agreed), a transition out
/// of a posted state (ledger invariant I3 being quietly weaker in the workflow than in the ledger), and a
/// decision whose reason was never written (a trail that cannot be reviewed).
/// </remarks>
public static class DocumentWorkflowEngine
{
    private static readonly IReadOnlyList<WorkflowError> NoErrors = [];

    /// <summary>
    /// Decides whether one document may move from where it is to where the actor is asking.
    /// </summary>
    /// <param name="definition">The declared life cycle of the document type.</param>
    /// <param name="subject">The document, including the decisions already recorded about it.</param>
    /// <param name="request">Who is deciding, what they want, when and why.</param>
    public static DocumentWorkflowDecision Evaluate(
        DocumentWorkflowDefinition definition,
        DocumentWorkflowSubject subject,
        DocumentWorkflowTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(request);

        var definitionErrors = definition.Validate();

        if (definitionErrors.Count > 0)
        {
            return Refused(definitionErrors);
        }

        if (definition.DocumentType != subject.DocumentType)
        {
            return Refused([new WorkflowError(
                WorkflowErrorCode.DocumentTypeMismatch,
                $"The definition is for {definition.DocumentType} but the document is {subject.DocumentType}.")]);
        }

        var current = definition.FindState(subject.CurrentStateCode);

        if (current is null)
        {
            return Refused([new WorkflowError(
                WorkflowErrorCode.UnknownCurrentState,
                $"'{subject.CurrentStateCode}' is not a state of {definition.DocumentType}, so the document's position is not knowable.")]);
        }

        var target = definition.FindState(request.ToStateCode);

        if (target is null)
        {
            return Refused([new WorkflowError(
                WorkflowErrorCode.UnknownTargetState,
                $"'{request.ToStateCode}' is not a state of {definition.DocumentType}.")]);
        }

        var historyError = CheckHistory(definition, subject, current);

        if (historyError is not null)
        {
            return Refused([historyError]);
        }

        var errors = new List<WorkflowError>();

        if (current.IsTerminal)
        {
            var correction = definition.ReversalDocumentType is { } reversal
                ? $"Correct it with a new {reversal} document, which is the same rule the ledger applies to a posted entry."
                : "This type declares no correcting document, so the position has to be taken up on a new document.";

            errors.Add(new WorkflowError(
                WorkflowErrorCode.TerminalStateRefused,
                $"'{current.Code}' is terminal: {correction}"));
        }

        var transition = definition.FindTransition(current.Code, target.Code);

        if (transition is null && !current.IsTerminal)
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.TransitionNotDeclared,
                $"The move '{current.Code}' to '{target.Code}' is not declared for {definition.DocumentType}. A path nobody wrote down is not a path."));
        }

        if (request.ActorUserId == Guid.Empty)
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.MissingActor,
                "A decision with no actor is not an approval, it is a blank line on the paper."));
        }

        if (transition is { Actor: WorkflowActorRequirement.NotDocumentCreator }
            && request.ActorUserId == subject.CreatorActorId)
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.SelfApprovalRefused,
                $"The creator of this {definition.DocumentType} cannot perform '{transition.To}': an approval from the person who made the commitment is the same signature twice."));
        }

        if (transition is { RequiresReason: true } && string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.ReasonRequired,
                $"'{transition.To}' must say why. The moves that need a reason are the ones a later reader cannot judge without one."));
        }

        if (request.CorrectsEntryId is { } correctedId && !ContainsEntry(subject, correctedId))
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.HistoryEntryNotFound,
                $"No recorded decision of this document has the id being corrected, so the reversal would link to nothing."));
        }

        if (errors.Count > 0)
        {
            return Refused(errors);
        }

        var entry = new DocumentWorkflowHistoryEntry
        {
            TenantId = subject.TenantId,
            DocumentType = subject.DocumentType,
            DocumentId = subject.DocumentId,
            EntryNumber = subject.History.Count + 1,
            FromState = current.Code,
            ToState = target.Code,
            ActorUserId = request.ActorUserId,
            OccurredAt = request.OccurredAt,
            Reason = request.Reason.Trim(),
            CorrectsEntryId = request.CorrectsEntryId,
        };

        return new DocumentWorkflowDecision(WorkflowDecisionStatus.Accepted, NoErrors, target, entry);
    }

    /// <summary>
    /// Checks that the document's state is the one its own trail of decisions ends at.
    /// </summary>
    /// <remarks>
    /// Cheap and brutal: a document whose state column says <c>APPROVED</c> while its last recorded
    /// decision says it was sent back has been edited behind the machine's back, and every rule that
    /// depends on the state — approval, editability, printability — is then being answered from a value
    /// nobody agreed to. Refusing here is what stops the trail and the state from becoming two definitions
    /// of one fact.
    /// </remarks>
    private static WorkflowError? CheckHistory(
        DocumentWorkflowDefinition definition,
        DocumentWorkflowSubject subject,
        DocumentStateDefinition current)
    {
        if (subject.History.Count == 0)
        {
            return string.Equals(subject.CurrentStateCode, definition.InitialStateCode, StringComparison.Ordinal)
                ? null
                : new WorkflowError(
                    WorkflowErrorCode.HistoryDoesNotMatchState,
                    $"A {definition.DocumentType} with no recorded decisions can only be in its initial state '{definition.InitialStateCode}', not '{subject.CurrentStateCode}'.");
        }

        foreach (var entry in subject.History)
        {
            if (entry.DocumentId != subject.DocumentId || entry.DocumentType != subject.DocumentType)
            {
                return new WorkflowError(
                    WorkflowErrorCode.HistoryDoesNotMatchState,
                    "This document's history contains an entry belonging to another document, so nothing in it can be trusted.");
            }
        }

        var last = subject.History[^1];

        return string.Equals(last.ToState, current.Code, StringComparison.Ordinal)
            ? null
            : new WorkflowError(
                WorkflowErrorCode.HistoryDoesNotMatchState,
                $"The last recorded decision ends in '{last.ToState}' but the document claims '{current.Code}'. One of the two was written by hand.");
    }

    private static bool ContainsEntry(DocumentWorkflowSubject subject, Guid entryId)
    {
        foreach (var entry in subject.History)
        {
            // An entry the engine produced but the store has not yet written has no id, so it cannot be
            // the target of a correction. Only a persisted decision is a decision.
            if (entry.Id != Guid.Empty && entry.Id == entryId)
            {
                return true;
            }
        }

        return false;
    }

    private static DocumentWorkflowDecision Refused(IReadOnlyList<WorkflowError> errors) =>
        new(WorkflowDecisionStatus.Refused, errors);
}
