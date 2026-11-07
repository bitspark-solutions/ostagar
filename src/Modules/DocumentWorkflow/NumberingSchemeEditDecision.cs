namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The outcome of asking whether a tenant may edit a numbering scheme they have already used.
/// </summary>
/// <param name="IsAccepted">Whether the proposed scheme may be saved. On false nothing is written.</param>
/// <param name="Errors">Each frozen field and why it is frozen, all at once, so the settings screen can
/// explain the whole problem instead of refusing one field per save.</param>
public sealed record NumberingSchemeEditDecision(
    bool IsAccepted,
    IReadOnlyList<NumberingError> Errors);
