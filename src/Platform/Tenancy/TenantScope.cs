using Ostagar.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// Opens a dependency-injection scope that is scoped to a tenant.
/// </summary>
/// <remarks>
/// The way a console command or a background job establishes tenancy. The session interceptor in the
/// data layer refuses to open a connection without a resolved tenant, so a job that forgot this would
/// fail at the first query instead of reading every company's rows — which is the behaviour the legacy
/// build needed and never had.
/// </remarks>
public static class TenantScope
{
    public static IServiceScope Open(IServiceScopeFactory scopeFactory, TenantId tenantId)
    {
        var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<AmbientTenantContext>().Set(tenantId);
        return scope;
    }

    /// <summary>
    /// Opens a scope for startup and maintenance work on global tables.
    /// </summary>
    /// <remarks>
    /// Call this by name at the call site rather than reaching for <c>EnterSystemScope</c> directly,
    /// so that every use is greppable. Seeding the permission catalogue is the legitimate case; reading
    /// tenant data through it is not, and row-level security still returns nothing if someone tries.
    /// </remarks>
    public static IServiceScope OpenSystem(IServiceScopeFactory scopeFactory)
    {
        var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<AmbientTenantContext>().EnterSystemScope();
        return scope;
    }
}
