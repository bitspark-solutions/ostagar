using System.Text;

namespace Ostagar.Modules.Partners;

/// <summary>
/// One identifier a partner holds, validated on entry so that no identifier in this system is a blob of
/// typed text.
/// </summary>
/// <remarks>
/// Identifiers are the only thing this module trusts when deciding whether two records describe one
/// business, so an unvalidated identifier is worse than none: <c>N/A</c> stored against a hundred suppliers
/// turns the duplicate check into a hundred false positives, and a TIN stored as <c>1234-5678-9</c> beside
/// the same number stored as <c>123456789</c> turns it into none. Values are normalised per kind,
/// placeholders are refused rather than stored, and the comparison form (<see cref="MatchKey"/>) is
/// derived, never typed.
///
/// This type is also why a partner cannot be created from a name alone: the aggregate demands at least one
/// identifier of a declared kind, and a name is not on that list.
/// </remarks>
public sealed record PartnerIdentifier
{
    public PartnerIdentifier(PartnerIdentifierKind kind, string value)
    {
        Kind = kind;
        Value = Validate(kind, value);
    }

    public PartnerIdentifierKind Kind { get; }

    public string Value { get; }

    /// <summary>
    /// Whether a match on this kind confirms a duplicate or merely raises one. A function of the kind, never
    /// of the value, so a caller cannot demote a trade licence to "suggestive" in order to get a record
    /// saved.
    /// </summary>
    public IdentifierAuthority Authority => AuthorityOf(Kind);

    /// <summary>
    /// The separator-free, uppercase form used for comparison. Deliberately not the stored value: a factory
    /// writes its ERC as <c>27/DHK/0123456</c> and its bank fills in <c>27 DHK 0123456</c>, and those are one
    /// licence, not two businesses.
    /// </summary>
    public string MatchKey => PartnerTextRules.AlphaNumericOnly(Value);

    /// <summary>True when both identifiers name the same register and hold the same register value.</summary>
    public bool Matches(PartnerIdentifier? other) =>
        other is not null && Kind == other.Kind && string.Equals(MatchKey, other.MatchKey, StringComparison.Ordinal);

    /// <summary>
    /// The authoritative set, stated once. Authoritative means issued to one business by an authority that
    /// does not knowingly issue twice and survives a rename, a move and a change of bank; everything else is
    /// suggestive because it demonstrably does not.
    /// </summary>
    public static IdentifierAuthority AuthorityOf(PartnerIdentifierKind kind) => kind switch
    {
        PartnerIdentifierKind.TradeLicenceNumber => IdentifierAuthority.Authoritative,
        PartnerIdentifierKind.TaxIdentificationNumber => IdentifierAuthority.Authoritative,
        PartnerIdentifierKind.VatRegistrationNumber => IdentifierAuthority.Authoritative,
        PartnerIdentifierKind.ExportRegistrationCertificateNumber => IdentifierAuthority.Authoritative,
        PartnerIdentifierKind.BankAccountNumber => IdentifierAuthority.Suggestive,
        PartnerIdentifierKind.PhoneNumber => IdentifierAuthority.Suggestive,
        PartnerIdentifierKind.EmailAddress => IdentifierAuthority.Suggestive,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown identifier kind."),
    };

    private static string Validate(PartnerIdentifierKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"{KindName(kind)} must not be empty.", nameof(value));
        }

        var normalised = kind switch
        {
            PartnerIdentifierKind.PhoneNumber => NormalisePhone(trimmed),
            PartnerIdentifierKind.EmailAddress => NormaliseEmail(trimmed),
            PartnerIdentifierKind.TaxIdentificationNumber or PartnerIdentifierKind.VatRegistrationNumber
                => StripSeparators(trimmed),
            _ => trimmed.ToUpperInvariant(),
        };

        var comparison = PartnerTextRules.AlphaNumericOnly(normalised);

        if (PartnerTextRules.IsPlaceholder(trimmed) || PartnerTextRules.IsPlaceholder(comparison))
        {
            throw new ArgumentException(
                $"'{trimmed}' is a placeholder, not a {KindName(kind)}. Record the real value or leave the field empty.",
                nameof(value));
        }

        var measuredLength = kind == PartnerIdentifierKind.EmailAddress ? normalised.Length : comparison.Length;

        var (minLength, maxLength) = kind switch
        {
            PartnerIdentifierKind.TaxIdentificationNumber => (6, 20),
            PartnerIdentifierKind.VatRegistrationNumber => (6, 24),
            PartnerIdentifierKind.TradeLicenceNumber => (4, 24),
            PartnerIdentifierKind.ExportRegistrationCertificateNumber => (4, 24),
            PartnerIdentifierKind.BankAccountNumber => (6, 34),
            PartnerIdentifierKind.PhoneNumber => (7, 15),
            PartnerIdentifierKind.EmailAddress => (6, 254),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown identifier kind."),
        };

        if (measuredLength < minLength || measuredLength > maxLength)
        {
            throw new ArgumentException(
                $"'{value}' is not a valid {KindName(kind)}: expected {minLength} to {maxLength} characters, "
                + $"got {measuredLength}.",
                nameof(value));
        }

        if (kind is PartnerIdentifierKind.TaxIdentificationNumber or PartnerIdentifierKind.VatRegistrationNumber
            && !comparison.All(char.IsDigit))
        {
            throw new ArgumentException(
                $"A {KindName(kind)} is digits only; '{value}' contains letters.",
                nameof(value));
        }

        if (kind == PartnerIdentifierKind.BankAccountNumber && !IsAlphanumericWithSeparators(normalised))
        {
            throw new ArgumentException(
                $"A {KindName(kind)} may contain only letters, digits and spaces; '{value}' contains other characters.",
                nameof(value));
        }

        if (kind is PartnerIdentifierKind.TradeLicenceNumber or PartnerIdentifierKind.ExportRegistrationCertificateNumber
            && !IsAlphanumericWithSeparators(normalised))
        {
            throw new ArgumentException(
                $"A {KindName(kind)} may contain only letters, digits, spaces, dashes, dots and slashes; '{value}' does not.",
                nameof(value));
        }

        return normalised;
    }

    private static string NormalisePhone(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsDigit(character))
            {
                builder.Append(character);
                continue;
            }

            var isCountryPrefix = character == '+' && builder.Length == 0;

            if (isCountryPrefix)
            {
                // Kept, not skipped. The error message below already promises "one leading +" is legal,
                // and a normaliser that accepts the country-code marker and then drops it makes
                // +8801711123456 and 8801711123456 the same number while leaving 01711123456 a different
                // one — so the marker has to survive into the match key to mean anything.
                builder.Append(character);
                continue;
            }

            if (character is not ('-' or ' ' or '(' or ')' or '.'))
            {
                throw new ArgumentException(
                    $"'{value}' is not a phone number; a phone number accepts digits, separators and one leading +.",
                    nameof(value));
            }
        }

        return builder.ToString();
    }

    private static string NormaliseEmail(string value)
    {
        var lower = value.ToLowerInvariant();
        var at = lower.IndexOf('@');
        var lastAt = lower.LastIndexOf('@');

        if (at <= 0 || at != lastAt || at == lower.Length - 1 || lower.Contains(' ', StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{value}' is not an e-mail address.", nameof(value));
        }

        var domain = lower[(at + 1)..];
        var dot = domain.IndexOf('.');

        if (dot <= 0 || dot == domain.Length - 1)
        {
            throw new ArgumentException(
                $"'{value}' has no top-level domain, so it is not a deliverable e-mail address.",
                nameof(value));
        }

        return lower;
    }

    private static string StripSeparators(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                builder.Append(character);
            }
            else if (!char.IsWhiteSpace(character) && character is not ('-' or '/' or '.'))
            {
                throw new ArgumentException(
                    $"'{value}' contains '{character}', which a tax or VAT number never does.",
                    nameof(value));
            }
        }

        return builder.ToString();
    }

    private static bool IsAlphanumericWithSeparators(string value) =>
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '/' or '.' or ' ');

    private static string KindName(PartnerIdentifierKind kind) => kind switch
    {
        PartnerIdentifierKind.TradeLicenceNumber => "trade licence number",
        PartnerIdentifierKind.TaxIdentificationNumber => "e-TIN",
        PartnerIdentifierKind.VatRegistrationNumber => "VAT registration number",
        PartnerIdentifierKind.ExportRegistrationCertificateNumber => "ERC number",
        PartnerIdentifierKind.BankAccountNumber => "bank account number",
        PartnerIdentifierKind.PhoneNumber => "phone number",
        PartnerIdentifierKind.EmailAddress => "e-mail address",
        _ => kind.ToString(),
    };
}
