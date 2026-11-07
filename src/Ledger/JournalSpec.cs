namespace Ostagar.Ledger;

/// <summary>
/// One journal as a template says it should exist, before it is a row.
/// </summary>
/// <param name="Code">Stable journal code, unique within the company.</param>
/// <param name="Name">English display name.</param>
/// <param name="NameBangla">Bangla display name.</param>
/// <param name="NumberPrefix">Prepended to every number this journal assigns.</param>
public sealed record JournalSpec(
    string Code,
    string Name,
    string NameBangla,
    string NumberPrefix);
