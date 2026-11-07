namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Which of the product's two written languages a label is being asked for.
/// </summary>
/// <remarks>
/// Two values, matching the <c>en</c> and <c>bn</c> message bundles the web app ships. Enumerating them
/// here rather than passing a culture name means a caller cannot ask for a language the product has not
/// committed to translating, and cannot silently receive English on a Bangla document.
/// </remarks>
public enum DocumentLanguage
{
    /// <summary>English, kept for reports, account codes and the audit file.</summary>
    English,

    /// <summary>Bangla, the operating language of the pilot factories.</summary>
    Bangla,
}
