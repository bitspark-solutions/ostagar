namespace Ostagar.Api.Controllers;

/// <summary>Payload for creating a department inside the caller's own company.</summary>
/// <param name="Code">Short stable identifier used on reports.</param>
/// <param name="Name">Display name, in English or transliterated.</param>
/// <param name="NameBangla">Display name in Bangla, shown when the user's language is Bangla.</param>
/// <param name="SortOrder">Position in picklists; lower sorts first.</param>
public sealed record CreateDepartmentRequest(
    string Code,
    string Name,
    string? NameBangla,
    int SortOrder);
