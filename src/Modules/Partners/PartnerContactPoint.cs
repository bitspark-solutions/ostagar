namespace Ostagar.Modules.Partners;

/// <summary>
/// A channel by which a partner can be reached, validated as that channel's format at entry.
/// </summary>
/// <remarks>
/// The value is not stored as typed. A phone number is normalised to digits so that <c>+880-1711-123456</c>
/// and <c>01711123456</c> are recognised as one number — which matters here because the phone number is one
/// of the suggestive identifiers the duplicate rule reads, and unnormalised it matches nothing. An e-mail
/// that is not an e-mail is refused, not accepted-and-flagged, because a malformed address produces a
/// delivery failure nobody can trace back to a master-data typo.
///
/// Contact values feed duplicate matching (<see cref="AsIdentifier"/>), so they are held to the same
/// validation as declared identifiers rather than being a spare note field.
/// </remarks>
public sealed record PartnerContactPoint
{
    public PartnerContactPoint(ContactKind kind, string value, string? attention = null, bool isPrimary = false)
    {
        Kind = kind;
        Value = Validate(kind, value);
        Attention = attention is null
            ? null
            : NullIfPlaceholder(PartnerTextRules.Normalise(attention, "Contact attention", PartnerTextRules.MaxNameLength));
        IsPrimary = isPrimary;
    }

    public ContactKind Kind { get; }

    public string Value { get; }

    /// <summary>Who answers this channel — "Merchandiser", "Accounts", "Export section".</summary>
    public string? Attention { get; }

    /// <summary>
    /// Whether a document should show this channel first. At most one per kind, enforced by
    /// <see cref="Partner.AddContactPoint"/>, because "the" phone number that comes out of a list ordering
    /// accident is exactly the kind of silent default this product refuses.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// The identifier this channel contributes to duplicate matching, or null when the kind is not one the
    /// rule reads. A website identifies nothing, so it matches nothing.
    /// </summary>
    public PartnerIdentifier? AsIdentifier() => Kind switch
    {
        ContactKind.Landline or ContactKind.Mobile or ContactKind.WhatsApp or ContactKind.Fax
            => new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, Value),
        ContactKind.Email => new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, Value),
        _ => null,
    };

    private static string Validate(ContactKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"A {kind} contact point needs a value.", nameof(value));
        }

        if (PartnerTextRules.IsPlaceholder(trimmed))
        {
            throw new ArgumentException(
                $"'{trimmed}' is a placeholder, not a {kind} contact.",
                nameof(value));
        }

        return kind switch
        {
            ContactKind.Email => new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, trimmed).Value,
            ContactKind.Website => ValidateWebsite(trimmed, value),
            _ => new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, trimmed).Value,
        };
    }

    private static string ValidateWebsite(string trimmed, string original)
    {
        var hasScheme = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var separator = trimmed.IndexOf("://", StringComparison.Ordinal);
        var host = hasScheme ? trimmed[(separator + 3)..] : trimmed;

        if (host.Length == 0 || host.Contains(' ', StringComparison.Ordinal) || !host.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{original}' is not a website address; expected a host name such as example.com.",
                nameof(original));
        }

        return (hasScheme ? trimmed : "https://" + trimmed).ToLowerInvariant();
    }

    private static string? NullIfPlaceholder(string normalised) =>
        normalised.Length == 0 ? null : normalised;
}
