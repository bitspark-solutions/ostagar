namespace Ostagar.Modules.Partners;

/// <summary>
/// A user-facing label in both of this product's languages, because a label with one language is not a
/// label for a Bangla-reading owner.
/// </summary>
/// <remarks>
/// Law L17 makes Bangla a first-class UI language rather than a bolt-on, and the precedent in this repo is
/// <c>BaseChartOfAccounts</c>, which carries <c>Name</c> and <c>NameBangla</c> on every account. The
/// vocabulary of a partner — "Vendor", "Partial advance", "Tax identification number" — is exactly as
/// user-facing as an account name, and if it exists only in English the frontend invents its own Bangla
/// string per screen, which is how one enum comes to have three spellings.
///
/// The web app may still route these through its own translation layer; what this pair guarantees is that
/// the domain vocabulary has one canonical Bangla form to route <i>to</i>. Both strings are culture-stable:
/// they are data, never formatted, never parsed.
/// </remarks>
/// <param name="English">The Latin-script label, used for reports and account codes.</param>
/// <param name="Bangla">The Bangla-script label shown in the Bangla UI.</param>
public sealed record LocalizedLabel(string English, string Bangla)
{
    public override string ToString() => English;
}
