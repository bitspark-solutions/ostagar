namespace Ostagar.Core;

/// <summary>
/// Identifies one tenant. Every tenant-scoped table, cache key, queue message, background job and
/// file path carries this value; the legacy trial-balance cache key carried none, so in a
/// multi-tenant deployment one company's financial report would have been served to another.
/// </summary>
public readonly record struct TenantId
{
    private readonly Guid _value;

    private TenantId(Guid value) => _value = value;

    /// <summary>
    /// The underlying uuid. Persistence and every cache key are built from this, never from a
    /// re-parsed string, so a tenant can never be addressed by two different textual forms.
    /// </summary>
    public Guid Value => _value;

    /// <summary>An unassigned tenant. Never valid for a persisted row.</summary>
    public static TenantId None { get; } = new(Guid.Empty);

    public bool IsEmpty => _value == Guid.Empty;

    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A tenant id must not be empty.", nameof(value))
            : new TenantId(value);

    public static TenantId Parse(string value) => From(Guid.Parse(value, System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Rejects both malformed input and the all-zeros guid, so a missing header can never silently
    /// become a tenant that owns rows.
    /// </summary>
    public static bool TryParse(string? value, out TenantId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new TenantId(parsed);
            return true;
        }

        result = None;
        return false;
    }

    public override string ToString() => _value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
