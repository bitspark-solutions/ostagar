using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// The only context that may open a database connection without a tenant.
/// </summary>
/// <remarks>
/// Resolving a login slug into a tenant id cannot be tenant-scoped — the tenant is what the lookup
/// produces — so <see cref="TenantSessionInterceptor"/> would rightly refuse to open a
/// connection on <see cref="OstagarDbContext"/> for it. This context exists instead of an escape
/// hatch inside that interceptor: it maps the directory table and nothing else, so a missing tenant
/// can never become a way to read a company's data.
/// </remarks>
public class TenantDirectoryDbContext(DbContextOptions<TenantDirectoryDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        TenantEntityConfiguration.Apply(modelBuilder);
        SnakeCaseModelNames.Apply(modelBuilder);
    }
}
