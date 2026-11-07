using Ostagar.Core;
using Ostagar.Platform.Tenancy;

namespace Ostagar.Platform.Rbac;

/// <summary>
/// Gives one role one permission inside one tenant.
/// </summary>
/// <remarks>
/// Tenant-owned even though it only links two identifiers: it is the record of what a factory decided
/// its accounts clerk may do, and tenant B must neither read nor overwrite that decision.
/// </remarks>
public class RoleGrant : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public required Guid RoleId { get; set; }

    public required int ResourceActionId { get; set; }

    public DateTimeOffset GrantedAt { get; set; }
}
