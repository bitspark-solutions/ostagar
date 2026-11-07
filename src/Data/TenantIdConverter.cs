using Ostagar.Core;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ostagar.Data;

/// <summary>
/// Stores <see cref="TenantId"/> as a Postgres <c>uuid</c>.
/// </summary>
/// <remarks>
/// The domain type is a wrapper so that an empty guid cannot travel far: it is rejected on the way in
/// and never read back as a real tenant. The provider side stays a plain uuid, so every index and
/// policy over <c>tenant_id</c> is a normal uuid comparison.
/// </remarks>
public sealed class TenantIdConverter : ValueConverter<TenantId, Guid>
{
    public TenantIdConverter()
        : base(tenantId => tenantId.Value, value => value == Guid.Empty ? TenantId.None : TenantId.From(value))
    {
    }
}
