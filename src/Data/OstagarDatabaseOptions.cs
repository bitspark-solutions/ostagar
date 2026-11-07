namespace Ostagar.Data;

/// <summary>
/// Everything about database connectivity a host has to decide.
/// </summary>
/// <remarks>
/// A class rather than loose configuration keys, because the difference between
/// <see cref="RequireTenantScopedPrincipal"/> being true and false is the difference between row-level
/// security being enforced and row-level security being silently bypassed by a superuser connection.
/// The legacy tree connected as the table owner and never noticed.
/// </remarks>
public sealed class OstagarDatabaseOptions
{
    public const string SectionName = "GarmentsDatabase";
    public const string DefaultConnectionStringsSection = "Default";
    public const string MigrationConnectionStringKey = "Migration";

    /// <summary>
    /// Connection used for reads and writes. It must be a principal that is neither the table owner nor
    /// a member of a bypassing role, or row-level security does not apply to it.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Connection used to apply migrations. A migration creates tables and hands ownership over, so it
    /// needs the owning principal; runtime must not use it. Null means the runtime connection is also
    /// used for migrations, which is right on a developer machine and wrong in a deployment.
    /// </summary>
    public string? MigrationConnectionString { get; init; }

    /// <summary>
    /// Fail at startup rather than at the first query when the runtime principal could step outside its
    /// tenant. Only switch this off where connecting as the owner is the point.
    /// </summary>
    public bool RequireTenantScopedPrincipal { get; init; } = true;

    /// <summary>
    /// Apply pending migrations when the host starts. Off by default: a schema change belongs to a
    /// deploy, not to whichever process came up first.
    /// </summary>
    public bool MigrateOnStartup { get; init; }

    public string RuntimeConnectionStringOrDefault() => ConnectionString;

    public string MigrationConnectionStringOrDefault() => MigrationConnectionString ?? ConnectionString;
}
