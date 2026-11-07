namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Machine-readable reasons a workflow transition was refused or a definition is broken.
/// </summary>
/// <remarks>
/// Codes, not sentences, so the same refusal can be shown in Bangla on the factory screen, asserted in a
/// test, and quoted in a support ticket without a string comparison breaking when the wording is
/// improved. The legacy validators returned free text, which meant a rule could not be tested at all.
/// </remarks>
public enum WorkflowErrorCode
{
    /// <summary>The same state code declared twice, so the machine has two rules for one name.</summary>
    DuplicateStateCode,

    /// <summary>A document cannot start in a state that does not exist.</summary>
    InitialStateNotDeclared,

    /// <summary>A transition names a state the definition never declared.</summary>
    TransitionStateNotDeclared,

    /// <summary>A state is printed in one language only. Law L17 refuses that.</summary>
    StateLabelIncomplete,

    /// <summary>A posted state that is not terminal: a rule weaker than ledger invariant I3.</summary>
    PostedStageMustBeTerminal,

    /// <summary>A posted document with no declared correcting document type, so an error has no way out.</summary>
    ReversalDocumentTypeMissing,

    /// <summary>The definition declares a way out of a terminal state. There is not one.</summary>
    TransitionFromTerminalState,

    /// <summary>The document claims a state this definition does not know.</summary>
    UnknownCurrentState,

    /// <summary>A definition for one document type was handed a document of another.</summary>
    DocumentTypeMismatch,

    /// <summary>The caller asked for a state that does not exist, rather than one that is merely disallowed.</summary>
    UnknownTargetState,

    /// <summary>No declared move between the two states. This is the ordinary refusal for a bad button.</summary>
    TransitionNotDeclared,

    /// <summary>The document is in a terminal state, so every transition is refused.</summary>
    TerminalStateRefused,

    /// <summary>No actor named. An audit trail that is silent about who decided is not a record.</summary>
    MissingActor,

    /// <summary>
    /// The actor who created the document is trying to approve it. Segregation of duties is not a UI
    /// concern: the approval line on the paper is the thing the buyer's accountant relies on.
    /// </summary>
    SelfApprovalRefused,

    /// <summary>A reason was demanded and nothing was written. Blank, not short.</summary>
    ReasonRequired,

    /// <summary>
    /// The document's own history does not end where it claims to be, so either the state column was
    /// edited directly or a transition was written without its history entry.
    /// </summary>
    HistoryDoesNotMatchState,

    /// <summary>The entry being corrected is not in this document's history.</summary>
    HistoryEntryNotFound,

    /// <summary>The document is in a state whose content is frozen. Send it back instead.</summary>
    DocumentNotEditable,
}
