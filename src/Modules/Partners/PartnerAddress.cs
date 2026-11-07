namespace Ostagar.Modules.Partners;

/// <summary>
/// One of a partner's addresses, owned by the partner and copied onto documents when used.
/// </summary>
/// <remarks>
/// Address lines are the one place in this module that genuinely is free text, and the distinction that
/// keeps that honest is what the text is allowed to <em>do</em>: nothing here derives a code, an account, a
/// duplicate decision or a posting from an address line. It is printed on a challan and stored for the VAT
/// file, and a typo in it costs a returned courier, not a corrupted ledger. Line 1 and city are mandatory
/// for exactly that reason — an address the product cannot route a document to is a blank wearing a label.
///
/// A partner holds several, of different <see cref="AddressKind"/>s, because the registered office, the
/// factory and the invoicing address are three different places for most Bangladeshi garment businesses.
/// </remarks>
public sealed record PartnerAddress
{
    public PartnerAddress(AddressKind kind, string line1, string city, string? line2 = null, string? district = null, string? postCode = null, string country = "BD")
    {
        Kind = kind;
        Line1 = PartnerTextRules.Normalise(line1, "Address line 1", PartnerTextRules.MaxAddressLineLength);
        City = PartnerTextRules.Normalise(city, "City or thana", PartnerTextRules.MaxAddressLineLength);

        if (Line1.Length == 0)
        {
            throw new ArgumentException("An address needs a first line.", nameof(line1));
        }

        if (City.Length == 0)
        {
            throw new ArgumentException("An address needs a city or thana.", nameof(city));
        }

        Line2 = NormaliseOptional(line2, "Address line 2");
        District = NormaliseOptional(district, "District");
        PostCode = NormaliseOptional(postCode, "Post code");
        Country = ValidateCountry(country);
    }

    public AddressKind Kind { get; }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string City { get; }

    public string? District { get; }

    public string? PostCode { get; }

    /// <summary>ISO 3166-1 alpha-2 country code. Bangladesh is the default, not the only value — fabric and trim vendors are imported.</summary>
    public string Country { get; }

    /// <summary>
    /// The single line a document header prints. Built in invariant order so a stored address never renders
    /// differently for two screens.
    /// </summary>
    public string SingleLine =>
        string.Join(", ", Parts());

    private IEnumerable<string> Parts()
    {
        yield return Line1;

        if (Line2 is { Length: > 0 })
        {
            yield return Line2;
        }

        if (District is { Length: > 0 } && !string.Equals(District, City, StringComparison.OrdinalIgnoreCase))
        {
            yield return District;
        }

        yield return City;

        if (PostCode is { Length: > 0 })
        {
            yield return PostCode;
        }

        if (!string.Equals(Country, "BD", StringComparison.Ordinal))
        {
            yield return Country;
        }
    }

    private static string? NormaliseOptional(string? value, string fieldName)
    {
        if (value is null)
        {
            return null;
        }

        var normalised = PartnerTextRules.Normalise(value, fieldName, PartnerTextRules.MaxAddressLineLength);

        if (normalised.Length == 0 || PartnerTextRules.IsPlaceholder(normalised))
        {
            return null;
        }

        return normalised;
    }

    private static string ValidateCountry(string value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (code.Length != 2 || !code.All(char.IsLetter))
        {
            throw new ArgumentException(
                $"'{value}' is not an ISO 3166-1 alpha-2 country code.",
                nameof(value));
        }

        return code;
    }
}
