using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Reads the tenant boundary straight off the EF model, so "which tables carry tenant data" has one
/// answer instead of a list someone has to remember to update.
/// </summary>
/// <remarks>
/// The isolation suite walks this to prove every table the model claims is tenant-owned also has
/// row-level security, <c>FORCE</c>, a check constraint and a composite index leading with
/// <c>tenant_id</c> in the real database. A new entity that forgets its tenancy then becomes a red test
/// rather than a quiet leak.
/// </remarks>
public static class TenantOwnedTables
{
    /// <summary>The CLR name of both the mapped and the shadow tenancy property.</summary>
    public const string PropertyName = "TenantId";

    /// <summary>The database name, after <see cref="SnakeCaseModelNames"/> has had its way with it.</summary>
    public const string PropertyColumnName = "tenant_id";

    /// <summary>
    /// The one policy name used everywhere. Written to <c>pg_policies</c> by the migration and asserted
    /// against it by the isolation suite, so a policy that got renamed or dropped is a test failure
    /// rather than a deployment surprise.
    /// </summary>
    public const string RowLevelSecurityPolicyName = "tenant_isolation";

    /// <summary>
    /// The schema every tenant-owned relation lives in. Asserted against <c>pg_namespace</c> so a table
    /// created in a different schema cannot be mistaken for a protected one.
    /// </summary>
    public const string SchemaName = "public";

    public static IReadOnlyList<IReadOnlyEntityType> OwnedByTenant(IReadOnlyModel model) =>
        model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(PropertyName) is not null)
            .ToArray();

    public static IReadOnlyList<string> TableNames(IReadOnlyModel model) =>
        OwnedByTenant(model)
            .Select(entityType => entityType.GetTableName())
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public static string CheckName(string tableName) => $"CK_{tableName}_tenant_scope";

    /// <summary>
    /// The predicate that binds a row to the session. Written once so that a policy added for a new
    /// table cannot invent a weaker one, and so the same text can be asserted against
    /// <c>pg_policies</c>.
    /// </summary>
    /// <remarks>
    /// <c>missing_ok = true</c> makes an unset session read as "no rows" instead of an error; the check
    /// constraint on the same expression makes an unset session an error instead of a silent empty
    /// tenant on writes; and <see cref="TenantSessionInterceptor"/> refuses to open the
    /// connection at all. Three different failure modes, and none of them is "worked, returned nothing".
    /// </remarks>
    public static string TenantScopePredicate() =>
        $"{PropertyColumnName} = current_setting('{TenantSessionInterceptor.TenantSettingName}', true)::uuid";
}
