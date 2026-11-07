using System.Globalization;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The whole life of one document type: its states, the moves between them, and which document type
/// carries the correction once the original is posted.
/// </summary>
/// <remarks>
/// A definition, not a base class. A new document type is added by writing one of these and, if its
/// states are unusual, a numbering scheme — nobody edits an engine, so nobody can add a path that skips
/// the approval rule. That is the difference between this and the legacy build, where each service
/// carried its own copy of "what does approved mean" and each copy drifted.
///
/// <see cref="ReversalDocumentType"/> is required for any type that can reach a posted state, because
/// "terminal" with no stated way out is a dead end rather than a rule: the accountant must know that the
/// correction is a credit note, exactly as the ledger's I3 makes the correction a reversal entry.
/// </remarks>
public sealed class DocumentWorkflowDefinition
{
    /// <summary>The document type this life cycle belongs to.</summary>
    public required DocumentTypeCode DocumentType { get; init; }

    /// <summary>Where a new document starts.</summary>
    public required string InitialStateCode { get; init; }

    public required IReadOnlyList<DocumentStateDefinition> States { get; init; }

    public required IReadOnlyList<DocumentTransition> Transitions { get; init; }

    /// <summary>
    /// Which document type corrects this one after it is posted. Null only for a type that can never be
    /// posted, such as an internal task list.
    /// </summary>
    public DocumentTypeCode? ReversalDocumentType { get; init; }

    public DocumentStateDefinition? FindState(string code) =>
        States.FirstOrDefault(state => string.Equals(state.Code, code, StringComparison.Ordinal));

    /// <summary>The declared move from one state to another, or null when the machine does not allow it.</summary>
    public DocumentTransition? FindTransition(string from, string to) =>
        Transitions.FirstOrDefault(transition =>
            string.Equals(transition.From, from, StringComparison.Ordinal)
            && string.Equals(transition.To, to, StringComparison.Ordinal));

    /// <summary>Everything the screen is allowed to offer a document in this state.</summary>
    public IReadOnlyList<DocumentTransition> TransitionsFrom(string from) =>
        [.. Transitions.Where(transition => string.Equals(transition.From, from, StringComparison.Ordinal))];

    /// <summary>The two printed names of a state, or null when the state is not declared.</summary>
    public BilingualLabel? LabelOf(string code) => FindState(code)?.Label;

    /// <summary>
    /// Every defect in the definition itself, checked before any document is moved.
    /// </summary>
    /// <remarks>
    /// Reported rather than thrown, so a mis-declared tenant definition surfaces as a refusal naming the
    /// defect instead of a 500 on someone's order screen — and so the test suite can assert that the
    /// standard definitions are clean.
    /// </remarks>
    public IReadOnlyList<WorkflowError> Validate()
    {
        var errors = new List<WorkflowError>();

        var seenStates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var state in States)
        {
            if (!seenStates.Add(state.Code))
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.DuplicateStateCode,
                    $"State '{state.Code}' is declared twice for {DocumentType}."));
            }

            if (!state.HasCompleteLabel)
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.StateLabelIncomplete,
                    $"State '{state.Code}' has no English and Bangla label pair, so it cannot be printed."));
            }

            if (state.Stage == DocumentLifecycleStage.Posted && !state.IsTerminal)
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.PostedStageMustBeTerminal,
                    $"State '{state.Code}' is posted but not terminal. A posted document that can move again is a weaker rule than the ledger's, and the ledger's is the one that is true."));
            }
        }

        if (FindState(InitialStateCode) is null)
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.InitialStateNotDeclared,
                $"The initial state '{InitialStateCode}' of {DocumentType} is not one of its declared states."));
        }

        if (ReversalDocumentType is null && States.Any(state => state.Stage == DocumentLifecycleStage.Posted))
        {
            errors.Add(new WorkflowError(
                WorkflowErrorCode.ReversalDocumentTypeMissing,
                $"{DocumentType} can be posted but declares no document type to carry its correction, so a mistake has no lawful route out."));
        }

        foreach (var transition in Transitions)
        {
            if (FindState(transition.From) is null)
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.TransitionStateNotDeclared,
                    $"A transition of {DocumentType} starts from undeclared state '{transition.From}'."));
            }

            if (FindState(transition.To) is null)
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.TransitionStateNotDeclared,
                    $"A transition of {DocumentType} ends in undeclared state '{transition.To}'."));
            }

            if (string.Equals(transition.From, transition.To, StringComparison.Ordinal))
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.TransitionStateNotDeclared,
                    $"A transition of {DocumentType} moves '{transition.From}' to itself, which records a decision without changing anything."));
            }

            var fromState = FindState(transition.From);

            if (fromState is { IsTerminal: true })
            {
                errors.Add(new WorkflowError(
                    WorkflowErrorCode.TransitionFromTerminalState,
                    $"'{transition.From}' is terminal but declares a move to '{transition.To}'. Corrections of a posted document are a new document of type {ReversalDocumentType?.Value ?? "(none)"}, not a transition out of it."));
            }
        }

        return errors;
    }
}
