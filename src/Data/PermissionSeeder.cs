using Ostagar.Core;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Creates the permission catalogue and the system roles a company starts with.
/// </summary>
/// <remarks>
/// This runs, and is allowed to run, on every start. The legacy build had the equivalent call
/// commented out in <c>Program.cs</c>, which made the entire <c>[RequirePermission]</c> layer
/// decorative: the attribute was real, the checks were real, and the table they read was empty.
/// Idempotent so that "wired into startup" is safe rather than something to remove once things work.
/// </remarks>
public sealed class PermissionSeeder(OstagarDbContext context)
{
    public async Task SeedCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var existing = await context.ResourceActions
            .IgnoreQueryFilters()
            .Select(action => new { action.Resource, action.Action })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var known = existing
            .Select(row => $"{row.Resource}.{row.Action}")
            .ToHashSet(StringComparer.Ordinal);

        var missing = PermissionCatalogue.All
            .Where(definition => !known.Contains(PermissionCatalogue.Key(definition.Resource, definition.Action)))
            .Select(definition => new ResourceAction
            {
                Resource = definition.Resource,
                Action = definition.Action,
                Description = definition.Description,
            });

        context.ResourceActions.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives one newly provisioned tenant its roles and their grants.
    /// </summary>
    /// <remarks>
    /// Called during provisioning rather than from a migration: a migration that back-filled roles
    /// would have to know every tenant that will ever exist, and the alternative — a global role
    /// table — is the same mistake as a global ledger.
    /// </remarks>
    public async Task SeedTenantRolesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId.IsEmpty)
        {
            throw new ArgumentException("Roles belong to a tenant; an empty tenant cannot have them.", nameof(tenantId));
        }

        foreach (var roleName in DefaultRoleMatrix.RoleNames)
        {
            var hasRole = await context.Roles.AnyAsync(role => role.Name == roleName, cancellationToken)
                .ConfigureAwait(false);

            if (!hasRole)
            {
                context.Roles.Add(new Platform.Identity.ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    DisplayName = roleName,
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
