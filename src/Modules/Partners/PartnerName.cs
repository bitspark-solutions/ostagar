namespace Ostagar.Modules.Partners;

/// <summary>
/// One of the three names a business partner is known by. Legal, trading and Bangla are separate fields
/// because they are separate facts, and collapsing them is what makes a rename destructive.
/// </summary>
/// <remarks>
/// A garment trading party routinely has: a name on its trade licence (the legal name, which is what a
/// court and the NBR see), a shorter name it prints on letterheads and stamps (the trading name, often
/// the only one the factory floor uses), and a Bangla rendering used on local documents and vehicle
/// invoices. The legacy tree had one <c>CustomerName</c> column, so a clerk recording "Rupganj Textiles
/// (Pvt) Ltd" against a licence held by "Rupganj Textiles Limited Private" had to choose, and whichever
/// they chose was wrong on one of the two documents that needed it.
///
/// This type deliberately knows nothing about which role the name plays — the aggregate decides that. It
/// only guarantees the text is storable, normalised, and written in a script this product supports.
/// </remarks>
public readonly record struct PartnerName
{
    private readonly string _value;

    private PartnerName(string value) => _value = value;

    /// <summary>The stored, normalised text.</summary>
    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    /// <summary>True when the name is written in Bangla script, wholly or partly.</summary>
    public bool IsBangla => ContainsBangla(_value);

    /// <summary>
    /// Parses and validates a name. Refuses empty text, text with no letter in it, placeholder text, and
    /// anything longer than <see cref="PartnerTextRules.MaxNameLength"/>.
    /// </summary>
    public static PartnerName From(string raw, string fieldName = "Name")
    {
        var normalised = PartnerTextRules.Normalise(raw, fieldName, PartnerTextRules.MaxNameLength);

        if (normalised.Length < 2)
        {
            throw new ArgumentException($"{fieldName} is too short to be a business name.", nameof(raw));
        }

        if (!PartnerTextRules.ContainsLetter(normalised))
        {
            throw new ArgumentException(
                $"{fieldName} has no letters in it, so it is not the name of a business.",
                nameof(raw));
        }

        if (PartnerTextRules.IsPlaceholder(normalised))
        {
            throw new ArgumentException(
                $"'{normalised}' is not a name, it is a blank. Record the party's real name or do not create the partner.",
                nameof(raw));
        }

        return new PartnerName(normalised);
    }

    public static bool TryFrom(string? raw, out PartnerName result, string fieldName = "Name")
    {
        try
        {
            result = From(raw ?? string.Empty, fieldName);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => Value;

    private static bool ContainsBangla(string? value) =>
        value is not null && PartnerTextRules.ContainsBanglaLetter(value);
}
