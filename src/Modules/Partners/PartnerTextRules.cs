using System.Text;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The character and shape rules every free-text field on a partner is put through before it is stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why Bangla script is allowed and checked rather than assumed.</b> The buyer of this product is a
/// Bangladeshi factory owner and the parties in their ledger are named <c>সোনালী কটন মিলস লিমিটেড</c>, not
/// <c>Sonali Cotton Mills Limited</c>. A validator built on <c>[A-Za-z]</c> would reject the real name of
/// most of a pilot tenant's customers, and the practical result of that is worse than an error: the clerk
/// types a Latin transliteration, the transliteration becomes the master record, and the factory owner
/// can no longer find the buyer they have traded with for twenty years. So the Bengali block
/// (U+0980–U+09FF, covering letters, matras, conjunct forms, Bengali digits and the taka sign) is a
/// first-class script here, on the same footing as Latin.
/// </para>
///
/// <para>
/// <b>What this does not license.</b> These rules validate <i>text</i>. They do not make free text an
/// identifier: no code, no account and no duplicate decision is ever derived from a string like this one.
/// That separation is the defect this module exists to close — the legacy tree generated chart-of-accounts
/// codes with <c>new Random().Next(100, 999)</c> from a typed-in category name, which is why nothing here
/// is creatable from a name alone.
/// </para>
/// </remarks>
public static class PartnerTextRules
{
    /// <summary>Longest name this product will print on a document header without asking for a shorter trade name.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Longest single address line. Two lines plus city, district and post code is what an invoice shows.</summary>
    public const int MaxAddressLineLength = 120;

    private static readonly string[] PlaceholderValues =
    [
        "NA", "N/A", "NAS", "NONE", "NIL", "NO", "TBD", "UNKNOWN", "TEST",
        "X", "XX", "XXX", "0", "00", "000", "-", "--", "?",
        "নাই", "নেই", "অজানা",
    ];

    /// <summary>
    /// Trims, collapses internal whitespace runs to a single space, and refuses control characters,
    /// over-long input and anything outside the scripts and punctuation a party name or address uses.
    /// </summary>
    /// <exception cref="ArgumentException">The text is empty, too long, or holds a character this product will not store.</exception>
    public static string Normalise(string value, string fieldName, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        var collapsed = new StringBuilder(value.Length);
        var lastWasSpace = true;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!lastWasSpace && collapsed.Length > 0)
                {
                    collapsed.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            if (char.IsControl(character))
            {
                throw new ArgumentException(
                    $"{fieldName} contains a control character, which is never part of a name or an address.",
                    nameof(value));
            }

            if (!IsAllowedCharacter(character))
            {
                throw new ArgumentException(
                    $"{fieldName} contains '{character}', which is outside the letters, digits and punctuation this product stores.",
                    nameof(value));
            }

            collapsed.Append(character);
            lastWasSpace = false;
        }

        var normalised = collapsed.ToString().Trim();

        if (normalised.Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} is {normalised.Length} characters; the limit is {maxLength}.",
                nameof(value));
        }

        return normalised;
    }

    public static bool ContainsLatinLetter(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Any(character => character < 0x0250 && char.IsLetter(character));
    }

    /// <summary>True when the text is written in Bangla script.</summary>
    public static bool ContainsBanglaLetter(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Any(character => character is >= '\u0980' and <= '\u09FF' && char.IsLetter(character));
    }

    /// <summary>
    /// A name has to say something. A run of punctuation or digits is what a form looks like when someone
    /// has typed into the wrong box, and it is not a business.
    /// </summary>
    public static bool ContainsLetter(string value) => ContainsLatinLetter(value) || ContainsBanglaLetter(value);

    /// <summary>
    /// Values that mean "we do not have this yet". They are refused rather than stored, because a stored
    /// <c>N/A</c> matches every other stored <c>N/A</c> and turns a duplicate check into a wall of false
    /// positives that everyone learns to click past.
    /// </summary>
    public static bool IsPlaceholder(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return PlaceholderValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Uppercased letters only, everything else dropped — the comparison form the code-vs-name guard uses.</summary>
    public static string LettersOnly(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Concat(value.Where(char.IsLetter).Select(character => char.ToUpperInvariant(character)));
    }

    /// <summary>Uppercased letters and digits only, every separator removed — how identifier values are compared.</summary>
    public static string AlphaNumericOnly(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Concat(value.Where(char.IsLetterOrDigit).Select(character => char.ToUpperInvariant(character)));
    }

    private static bool IsAllowedCharacter(char character)
    {
        if (character is >= '\u0980' and <= '\u09FF')
        {
            return true;
        }

        if (character < 0x0250 && (char.IsLetter(character) || char.IsDigit(character)))
        {
            return true;
        }

        return character is
            '.' or ',' or '\'' or '’' or '"' or '&' or '/' or '(' or ')' or '-' or '+' or ':' or ';';
    }
}
