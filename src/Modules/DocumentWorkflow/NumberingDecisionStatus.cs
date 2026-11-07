namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// How a numbering attempt ended. Three values, because "we gave a number" and "we gave back the number
/// we already gave" are different facts and a caller must be able to tell them apart.
/// </summary>
/// <remarks>
/// <see cref="Replayed"/> is not reported as <see cref="Allocated"/>. A caller that believes it just
/// consumed a value will advance its own bookkeeping again; a caller that knows the value was already
/// consumed retries safely. Collapsing the two is how a payment pressed twice becomes two payments.
/// </remarks>
public enum NumberingDecisionStatus
{
    /// <summary>A value was consumed and a number rendered with it.</summary>
    Allocated,

    /// <summary>The same allocation key on the same counter: the original number, and nothing advanced.</summary>
    Replayed,

    /// <summary>No number, and the counter must not be written.</summary>
    Refused,
}
