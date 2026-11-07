namespace Ostagar.Modules.Partners;

/// <summary>
/// The partner's name in Bangla script, as its own field rather than an optional extra on the Latin name.
/// </summary>
/// <remarks>
/// Bangla is a first-class UI language for this product (law L17), and a party's Bangla name is the form
/// that appears on a local challan, a VAT invoice issued in Bangla, and the screen a Bangla-reading owner
/// actually scans. Keeping it in a separate field means the Latin legal name stays intact for the bank,
/// the letter of credit and the export documentation, which need it exactly as registered.
///
/// The field enforces its own meaning: it refuses text with no Bangla letter in it. Latin transliterations
/// are legitimate data, but they belong in the legal or trading name — storing <c>Sonali Cotton</c> here
/// would produce a "Bangla name" column that is silently English in most rows, which is how a first-class
/// language quietly becomes a nullable spare field again.
/// </remarks>
public readonly record struct BanglaName
{
    private readonly string _value;

    private BanglaName(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    /// <summary>Parses and validates a Bangla name, refusing text that is not written in Bangla script.</summary>
    public static BanglaName From(string raw)
    {
        var normalised = PartnerTextRules.Normalise(raw, "Bangla name", PartnerTextRules.MaxNameLength);

        if (!PartnerTextRules.ContainsBanglaLetter(normalised))
        {
            throw new ArgumentException(
                $"'{normalised}' is not written in Bangla script. A transliteration belongs in the legal or trading name; "
                + "this field holds the name as the party writes it in Bangla.",
                nameof(raw));
        }

        if (PartnerTextRules.IsPlaceholder(normalised))
        {
            throw new ArgumentException(
                $"'{normalised}' is a blank, not a name.",
                nameof(raw));
        }

        return new BanglaName(normalised);
    }

    public static bool TryFrom(string? raw, out BanglaName result)
    {
        try
        {
            result = From(raw ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
