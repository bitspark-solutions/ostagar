using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Lets the EF tooling build the context without going through the application's composition root.
/// </summary>
/// <remarks>
/// Migrations are DDL against the whole schema, not rows inside one tenant, so they cannot carry a
/// tenant — and <see cref="TenantSessionInterceptor"/> rightly refuses to open a connection
/// that does. Without this factory, <c>dotnet ef database update</c> fails on the tenancy guard and
/// the tempting wrong fix is to relax the guard. The runtime path keeps the interceptor; only the
/// design-time path skips it, and it is reached solely by the EF tools.
///
/// It reads the same environment variable the host does, and deliberately has no fallback: a
/// migration applied against a database nobody intended is worse than a migration that refuses to
/// run.
/// </remarks>
public sealed class OstagarDbContextFactory : IDesignTimeDbContextFactory<OstagarDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Default";

    public OstagarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} must be set to run migrations. It is the connection "
                + "of the owning principal, not the runtime one, and guessing it is not a fallback.");

        var options = new DbContextOptionsBuilder<OstagarDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Migrations touch global schema, not one tenant's rows, so they run in an explicit system
        // scope rather than a bare unscoped context — which the session interceptor would refuse.
        var tenantContext = new Platform.Tenancy.AmbientTenantContext();
        tenantContext.EnterSystemScope();

        return new OstagarDbContext(options, tenantContext);
    }
}
