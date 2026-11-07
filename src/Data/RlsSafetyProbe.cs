using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ostagar.Data;

/// <summary>
/// Checks, against the live database, that the connection the application actually uses cannot step
/// outside its tenant.
/// </summary>
/// <remarks>
/// Row-level security is only a guarantee if the principal issuing the query is subject to it. A
/// superuser, a role holding BYPASSRLS, or the table owner sees every tenant's rows no matter what the
/// policy says — which is how the legacy build let any Manager read any company's ledger while the
/// policies looked correct in the migration. This probe turns that into a startup failure instead of a
/// deployment detail nobody checks.
///
/// It reads the schema as well as the role: every table the model claims is tenant-owned must have
/// row-level security enabled, forced, and a policy with the exact name the migration creates. A
/// migration that was never applied, or a policy that was edited by hand, fails here rather than
/// showing a plausible-looking empty screen to the customer.
/// </remarks>
public sealed class RlsSafetyProbe(OstagarDatabaseOptions options)
{
    public async Task EnsureSafeAsync(CancellationToken cancellationToken = default)
    {
        var tenantTables = TenantOwnedTables.TableNames(ModelOnlyContext().Model);
        var violations = new List<string>();

        await using var connection = new NpgsqlConnection(options.RuntimeConnectionStringOrDefault());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT coalesce(bool_or(role.rolsuper), false),
                       coalesce(bool_or(role.rolbypassrls), false)
                FROM pg_roles role
                WHERE role.rolname = current_role
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (reader.GetBoolean(0))
            {
                violations.Add("the runtime database principal is a superuser, which bypasses row-level security");
            }

            if (reader.GetBoolean(1))
            {
                violations.Add("the runtime database principal holds BYPASSRLS");
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT relation.relname,
                       relation.relrowsecurity,
                       relation.relforcerowsecurity,
                       pg_get_userbyid(relation.relowner),
                       pg_has_role(current_role, relation.relowner, 'USAGE'),
                       policy.policyname
                FROM pg_class relation
                JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
                LEFT JOIN pg_policies policy
                  ON policy.schemaname = namespace.nspname
                 AND policy.tablename = relation.relname
                 AND policy.policyname = @policyName
                WHERE namespace.nspname = @schemaName
                  AND relation.relkind = 'r'
                  AND relation.relname = ANY (@tables)
                """;

            command.Parameters.AddWithValue("policyName", TenantOwnedTables.RowLevelSecurityPolicyName);
            command.Parameters.AddWithValue("schemaName", TenantOwnedTables.SchemaName);
            command.Parameters.AddWithValue("tables", tenantTables.ToArray());

            var seenTables = new HashSet<string>(StringComparer.Ordinal);

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var tableName = reader.GetString(0);
                    var enabled = reader.GetBoolean(1);
                    var forced = reader.GetBoolean(2);
                    var owner = reader.GetString(3);
                    var principalIsOwner = reader.GetBoolean(4);
                    var hasPolicy = !reader.IsDBNull(5);

                    seenTables.Add(tableName);

                    if (!enabled || !forced || !hasPolicy)
                    {
                        violations.Add(
                            $"table '{tableName}' is not fully protected: row security enabled={enabled}, "
                            + $"forced={forced}, policy '{TenantOwnedTables.RowLevelSecurityPolicyName}' present={hasPolicy}");
                    }

                    // Ownership, including through role membership: an owner reads every row unless the
                    // table is FORCEd, and even then it is one SET ROLE away from a bypass.
                    if (principalIsOwner)
                    {
                        violations.Add($"table '{tableName}' is owned by '{owner}', which the runtime principal is part of");
                    }
                }
            }

            foreach (var tableName in tenantTables)
            {
                if (!seenTables.Contains(tableName))
                {
                    violations.Add($"table '{tableName}' is missing from the database");
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Tenant isolation is not enforced by this connection. "
                + string.Join("; ", violations)
                + ". Connect with the application role the migration creates, not as its owner.");
        }
    }

    /// <summary>
    /// The model, without a live connection: it is what "tenant-owned" is defined by, and reading it
    /// must not require the tenant that has not been established yet.
    /// </summary>
    private static OstagarDbContext ModelOnlyContext() => new(
        new DbContextOptionsBuilder<OstagarDbContext>()
            .UseNpgsql("Host=localhost;Database=ostagar;Username=garments;Password=garments")
            .Options,
        new Platform.Tenancy.AmbientTenantContext());
}
