using System.Globalization;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The stable identity of one business partner. Never regenerated, never re-derived from a name.
/// </summary>
/// <remarks>
/// A garment factory's buyer list is a decade of phonebooks, VAT files and shipping documents, and the
/// party in it gets renamed: a sole trader becomes a limited company, a factory adds "Exports" to its
/// name, a spelling is corrected. Every one of those is a rename, not a new business, and the rewrite's
/// legacy tree handled renames by letting people create a second record — which is how one real buyer
/// ended up with two aged-receivables lines and nobody could say which was the money.
///
/// This type is the answer to that: the identifier is minted once at registration and no operation on
/// <see cref="Partner"/> can change it. Codes, names, roles, addresses, bank accounts and terms all move
/// around it while it stays.
///
/// It is a distinct type from <see cref="Guid"/> rather than a bare guid so that an empty guid cannot be
/// passed where a partner is meant — the legacy services accepted <c>Guid.Empty</c> and answered
/// "not found" as if that were a normal outcome.
/// </remarks>
public readonly record struct PartnerId
{
    private readonly Guid _value;

    private PartnerId(Guid value) => _value = value;

    /// <summary>The underlying uuid. Persistence and every cache key are built from this.</summary>
    public Guid Value => _value;

    /// <summary>An unassigned partner. Never valid on a persisted row or a document line.</summary>
    public static PartnerId None => new(Guid.Empty);

    public bool IsEmpty => _value == Guid.Empty;

    /// <summary>
    /// Mints a new identity. Only the registration path may call this, and only when it is about to
    /// persist the partner; a caller that needs an id for something it has not stored has no business
    /// here.
    /// </summary>
    public static PartnerId New() => new(Guid.NewGuid());

    public static PartnerId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A partner id must not be empty.", nameof(value))
            : new PartnerId(value);

    /// <summary>Rejects malformed input and the all-zeros guid, so a missing id never becomes a partner.</summary>
    public static bool TryParse(string? value, out PartnerId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new PartnerId(parsed);
            return true;
        }

        result = None;
        return false;
    }

    public override string ToString() => _value.ToString("D", CultureInfo.InvariantCulture);
}
