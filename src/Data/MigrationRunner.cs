using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Applies pending migrations using the migration principal.
/// </summary>
/// <remarks>
/// Constructed here rather than resolved from the host: a migration connection must not carry the
/// tenant session interceptor, and the tenant of a schema change is nobody in particular. Keeping it a
/// separate object also keeps <c>MigrateOnStartup</c> from quietly becoming "the web process owns the
/// schema".
/// </remarks>
public sealed class MigrationRunner(OstagarDatabaseOptions options)
{
    public async Task<IReadOnlyList<string>> ApplyPendingAsync(CancellationToken cancellationToken = default)
    {
        var context = CreateContext(options.MigrationConnectionStringOrDefault());
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var applied = pending.ToArray();

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        return applied;
    }

    private static OstagarDbContext CreateContext(string connectionString) =>
        new(
            new DbContextOptionsBuilder<OstagarDbContext>()
                .UseNpgsql(connectionString)
                .Options,
            new Platform.Tenancy.AmbientTenantContext());
}
