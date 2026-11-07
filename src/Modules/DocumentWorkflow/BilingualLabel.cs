namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The two written forms a user-facing label must ship with, in this order, always both.
/// </summary>
/// <remarks>
/// A pair rather than a resource key, because the state name a document prints travels with the
/// document: a PDF of an invoice approved in 2026 has to keep saying what it said then, even after the
/// translation table is reworded, and a buyer's accountant in Dhaka and their auditor in Dhaka both read
/// the same paper. Law L17 makes Bangla first-class rather than a bolt-on, and a record that physically
/// cannot hold one language is the cheapest way to make that true without a CI rule nobody reads.
///
/// Nothing here does culture-aware formatting of numbers or dates — that stays with
/// <see cref="System.Globalization.CultureInfo"/> at the render edge — this carries words.
/// </remarks>
/// <param name="English">The label in English, which reports and account codes keep using.</param>
/// <param name="Bangla">The label in Bangla, the language the factory floor actually runs in.</param>
public readonly record struct BilingualLabel(string English, string Bangla)
{
    /// <summary>False when either form is missing, which makes a document unsafe to print.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(English) && !string.IsNullOrWhiteSpace(Bangla);

    public string For(DocumentLanguage language) => language == DocumentLanguage.Bangla ? Bangla : English;

    public override string ToString() => $"{English} / {Bangla}";
}
