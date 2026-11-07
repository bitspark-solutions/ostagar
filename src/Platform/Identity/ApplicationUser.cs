using Ostagar.Core;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace Ostagar.Platform.Identity;

/// <summary>
/// A login that belongs to exactly one tenant. The same email address may exist in two tenants without
/// either one learning that the other exists.
/// </summary>
/// <remarks>
/// Identity's uniqueness rules are global, which is wrong for a shared schema: the model replaces them
/// with a composite over <c>tenant_id</c>, and row-level security means the duplicate check can only
/// ever see rows the caller is already allowed to see.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>, ITenantScoped
{
    public TenantId TenantId { get; set; }

    public required string FullName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
