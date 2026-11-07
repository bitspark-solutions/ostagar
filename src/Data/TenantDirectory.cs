using Ostagar.Core;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Directory access over the tenant directory context.
/// </summary>
public sealed class TenantDirectory(TenantDirectoryDbContext context) : ITenantDirectory
{
    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("A tenant lookup needs a slug.", nameof(slug));
        }

        return context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Slug == slug.Trim())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Tenant?> FindAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        context.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (tenant.Id.IsEmpty)
        {
            throw new ArgumentException("A tenant must be created with its id already set.", nameof(tenant));
        }

        // The id exists before the insert rather than being read back after it: every later write in the
        // same provisioning run opens its connection with app.tenant_id carrying this value, and there is
        // no second round trip that could half-provision a company.
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tenant;
    }
}
