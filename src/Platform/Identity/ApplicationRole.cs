using Ostagar.Core;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace Ostagar.Platform.Identity;

/// <summary>
/// A role owned by one tenant. Roles are per tenant on purpose: a factory's Accounts clerk must not
/// gain rights because a different tenant granted its own Accounts role something new.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>, ITenantScoped
{
    public TenantId TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Roles created by the tenant provisioner are system roles: they carry the permission matrix, they
    /// cannot be deleted, and only their grants can be re-granted.
    /// </summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
