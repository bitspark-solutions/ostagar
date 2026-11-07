namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// What happened when a document asked for its number.
/// </summary>
/// <remarks>
/// <see cref="Sequence"/> is the row the integrator writes back, not the row it read. Returning the new
/// state is what keeps the allocation pure: the allocator cannot advance a counter in place and then be
/// unsure whether the transaction that holds it commits, which is the shape of the legacy bug where the
/// number was written before the document was validated and a rejected save left the series with a hole.
/// On a refusal <see cref="Sequence"/> is null and nothing may be persisted.
/// </remarks>
/// <param name="Status">How the attempt ended.</param>
/// <param name="Errors">Every rule that refused the number. Empty unless <paramref name="Status"/> is
/// <see cref="NumberingDecisionStatus.Refused"/>.</param>
/// <param name="Number">The rendered number, for the caller to print and store.</param>
/// <param name="Sequence">The counter to persist, or null when nothing was consumed.</param>
public sealed record NumberingDecision(
    NumberingDecisionStatus Status,
    IReadOnlyList<NumberingError> Errors,
    string? Number = null,
    NumberingSequenceState? Sequence = null)
{
    public bool Succeeded => Status != NumberingDecisionStatus.Refused;
}
