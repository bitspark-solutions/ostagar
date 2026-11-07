using Ostagar.Core;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// Reads and writes the tenant directory — the table that says which companies exist.
/// </summary>
/// <remarks>
/// The one data path that runs without a resolved tenant, because resolving a login slug into a tenant
/// id is how a tenant becomes known in the first place. It is served by a separate context type rather
/// than an escape hatch inside the session interceptor, so that "this read is not tenant scoped" stays
/// a fact about the code, visible in review, instead of a mode someone can forget to exit.
/// </remarks>
public interface ITenantDirectory
{
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Tenant?> FindAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
