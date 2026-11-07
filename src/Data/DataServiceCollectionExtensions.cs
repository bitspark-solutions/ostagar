using Ostagar.Ledger;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Data;

/// <summary>
/// Registers the two database contexts and the tenant plumbing that goes with them.
/// </summary>
/// <remarks>
/// <c>AddDbContext</c> for both, never <c>AddDbContextPool</c>: <c>app.tenant_id</c> is session state on
/// a Postgres connection, and a pooled context hands one company's session — and its tracked entities —
/// to the next request. The legacy tree pooled at 128 with no reset per open, which is the same bug as
/// having no tenancy column at all, only harder to see.
/// </remarks>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddGarmentsData(this IServiceCollection services, OstagarDatabaseOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(selector => selector.GetRequiredService<AmbientTenantContext>());

        services.AddDbContext<OstagarDbContext>((provider, builder) =>
        {
            builder.UseNpgsql(options.RuntimeConnectionStringOrDefault());
            builder.AddInterceptors(
                new TenantSessionInterceptor(provider.GetRequiredService<ITenantContext>()),
                new TenantSaveChangesInterceptor(provider.GetRequiredService<ITenantContext>()));
        });

        // No interceptor here, on purpose: this is the context that has to work before a tenant is known.
        services.AddDbContext<TenantDirectoryDbContext>(builder =>
            builder.UseNpgsql(options.RuntimeConnectionStringOrDefault()));

        services.AddScoped<ITenantDirectory, TenantDirectory>();
        services.AddScoped<IPoster, Poster>();
        services.AddScoped<ILedgerProvisioner, LedgerProvisioner>();
        services.AddScoped<IOpeningBalances, OpeningBalances>();
        services.AddScoped<IExchangeRates, ExchangeRates>();
        services.AddScoped<Platform.Audit.IAuditWriter, DatabaseAuditWriter>();
        services.AddScoped<MigrationRunner>();
        services.AddSingleton<RlsSafetyProbe>();

        return services;
    }

    /// <summary>
    /// Reads the settings a host needs, without inventing any of them.
    /// </summary>
    public static OstagarDatabaseOptions ReadDatabaseOptions(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string connectionStringsSection = OstagarDatabaseOptions.DefaultConnectionStringsSection)
    {
        var runtime = configuration.GetConnectionString(connectionStringsSection)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{connectionStringsSection} is not configured, and every tenant-scoped "
                + "query needs a database to be refused against.");

        var settings = configuration.GetSection(OstagarDatabaseOptions.SectionName);
        var configuredRole = settings[nameof(OstagarDatabaseOptions.RequireTenantScopedPrincipal)];

        return new OstagarDatabaseOptions
        {
            ConnectionString = runtime,
            MigrationConnectionString = configuration.GetConnectionString(OstagarDatabaseOptions.MigrationConnectionStringKey),
            RequireTenantScopedPrincipal = !bool.TryParse(configuredRole, out var require) || require,
            MigrateOnStartup = bool.TryParse(settings[nameof(OstagarDatabaseOptions.MigrateOnStartup)], out var migrate) && migrate,
        };
    }
}
