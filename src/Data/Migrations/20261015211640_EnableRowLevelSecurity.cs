using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations;

/// <summary>
/// Creates the two database principals and puts row-level security on every tenant-owned table.
/// </summary>
/// <remarks>
/// This migration is the reason the rest of the tenancy design is real rather than aspirational.
/// Row-level security does not apply to a superuser or to a role holding BYPASSRLS, and the
/// application was connecting as <c>postgres</c> — so without a dedicated runtime principal, every
/// policy written here would be silently ignored and the isolation suite would pass while
/// protecting nothing. That is the "worked, returned nothing" failure this project is built to
/// refuse, in its most expensive form.
///
/// Ownership and runtime privilege are split deliberately: <c>garments_owner</c> creates and alters,
/// <c>garments_app</c> reads and writes and is subject to the policies. <c>FORCE ROW LEVEL
/// SECURITY</c> is applied anyway so that even a misconfigured deployment where the owner connects
/// cannot read across tenants.
/// </remarks>
public partial class EnableRowLevelSecurity : Migration
{
    /// <summary>Tables carrying <c>tenant_id</c>. The directory and the permission catalogue are not here on purpose.</summary>
    private static readonly string[] TenantScopedTables =
    [
        "audit_logs", "company_profiles", "departments", "roles", "users",
        "role_claims", "role_grants", "user_claims", "user_logins", "user_roles", "user_tokens",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Created idempotently: Postgres has no CREATE ROLE IF NOT EXISTS, and a migration that
        // cannot be re-run after a partial failure is a migration that gets hand-edited under
        // pressure. Roles are cluster-wide, so a fresh database on an existing server hits this.
        foreach (var statement in new[]
                 {
                     "DO $$ BEGIN CREATE ROLE garments_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE; EXCEPTION WHEN duplicate_object THEN NULL; END $$;",
                     "DO $$ BEGIN CREATE ROLE garments_app NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; EXCEPTION WHEN duplicate_object THEN NULL; END $$;",
                 })
        {
            migrationBuilder.Sql(statement);
        }

        foreach (var table in TenantScopedTables)
        {
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" OWNER TO garments_owner;");
            migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON public.\"{table}\" TO garments_app;");

            // FORCE makes the policies bind the table owner too. Without it, a deployment that
            // connects as the owner — which is exactly what a developer does by default — would
            // see every tenant's rows and every test would still be green.
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" FORCE ROW LEVEL SECURITY;");

            migrationBuilder.Sql($"""
                DROP POLICY IF EXISTS "tenant_isolation" ON public."{table}";
                CREATE POLICY "tenant_isolation" ON public."{table}"
                    AS PERMISSIVE
                    FOR ALL
                    TO garments_app
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        // The directory and the permission catalogue get privileges but no policy. Enabling RLS on
        // them without one would deny every read and break login resolution — the tenant is what the
        // directory lookup produces, so it cannot itself be tenant-scoped. Keeping them out is the
        // honest representation of that, rather than a policy that pretends otherwise.
        foreach (var table in new[] { "tenants", "resource_actions" })
        {
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" OWNER TO garments_owner;");
            migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON public.\"{table}\" TO garments_app;");
        }

        migrationBuilder.Sql("""
            ALTER DEFAULT PRIVILEGES FOR ROLE garments_owner IN SCHEMA public
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO garments_app;
            """);

        // The migration history table is written by the deploying principal, not the runtime one.
        migrationBuilder.Sql("GRANT USAGE ON SCHEMA public TO garments_app;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantScopedTables)
        {
            migrationBuilder.Sql($"DROP POLICY IF EXISTS \"tenant_isolation\" ON public.\"{table}\";");
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DISABLE ROW LEVEL SECURITY;");
        }

        migrationBuilder.Sql("REVOKE ALL ON SCHEMA public FROM garments_app;");

        foreach (var table in TenantScopedTables.Concat(new[] { "tenants", "resource_actions" }))
        {
            migrationBuilder.Sql($"REVOKE ALL ON public.\"{table}\" FROM garments_app;");
        }
    }
}
