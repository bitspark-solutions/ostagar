namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One state a document type can be in, with the stage it belongs to and the two names printed on it.
/// </summary>
/// <remarks>
/// <see cref="Label"/> is carried here rather than looked up from a translation table, because the state
/// name appears on printed documents. A paper invoice from 2026 must keep saying <c>APPROVED</c> /
/// <c>অনুমোদিত</c> even after the words are reworded, and a reword that changes what a signed document
/// says is not a translation change but an amendment.
///
/// <see cref="Code"/> is per-tenant-stable text rather than an enum for the same reason
/// <see cref="DocumentTypeCode"/> is: factories arrive with their own state vocabulary already on paper,
/// and a new name must not require a build.
/// </remarks>
/// <param name="Code">Stable machine name of the state, unique inside one workflow definition.</param>
/// <param name="Stage">What the rules treat it as, which decides editability and reversibility.</param>
/// <param name="Label">English and Bangla, both required for anything printable.</param>
/// <param name="IsTerminal">No transition may leave it. A posted document is corrected by reversing it,
/// not by moving it.</param>
public sealed record DocumentStateDefinition(
    string Code,
    DocumentLifecycleStage Stage,
    BilingualLabel Label,
    bool IsTerminal = false)
{
    /// <summary>True when the state's label can legally appear on a printed document.</summary>
    public bool HasCompleteLabel => Label.IsComplete;
}
