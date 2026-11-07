using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations;

/// <summary>
/// Makes the audit trail append-only, which is the property its name promises and the one the schema did
/// not have.
/// </summary>
/// <remarks>
/// <para>
/// The earlier row-level-security migration granted <c>SELECT, INSERT, UPDATE, DELETE</c> on every
/// tenant-owned table, <c>audit_logs</c> included, and enabled the same <c>tenant_isolation</c> policy on
/// it as on a departments table. That protects one company's rows from another and nothing else: it says
/// nothing about whether a row may be changed or removed once it exists. So the claim that the trail was
/// tamper-evident had no mechanism behind it — an <c>UPDATE</c> on <c>audit_logs</c> was simply a
/// statement that worked.
/// </para>
///
/// <para>
/// <b>Two measures, because one is not enough.</b> <c>REVOKE</c> removes the privilege from the runtime
/// role, and is the right shape: a denial costs nothing, writes nothing, and cannot be reached by a code
/// path that forgets to call a trigger. But <c>garments_owner</c> owns the table and ownership outranks
/// a grant, so a deployment that connects as the owner — which is exactly what a developer does by
/// default, and what this project's own gate does — could still edit history. The trigger refuses for
/// everyone, including the owner, and is the second line rather than the first.
/// </para>
///
/// <para>
/// Correction is by appending: a wrong audit entry stays, and the entry that supersedes it names it.
/// That is the same rule the ledger applies to itself under invariant I3, for the same reason — the
/// record of a mistake is the only evidence that the mistake was ever there.
/// </para>
/// </remarks>
public partial class MakeAuditTrailAppendOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            REVOKE UPDATE, DELETE ON public."audit_logs" FROM garments_app;
            """);

        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reject_audit_log_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION
                    'The audit trail is append-only: % of audit row % is not permitted. Append the entry that supersedes it instead.',
                    lower(TG_OP), OLD.id;
            END
            $$;

            DROP TRIGGER IF EXISTS CK_audit_logs_append_only ON audit_logs;
            CREATE TRIGGER CK_audit_logs_append_only
                BEFORE UPDATE OR DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION reject_audit_log_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS CK_audit_logs_append_only ON audit_logs;
            DROP FUNCTION IF EXISTS reject_audit_log_mutation();

            GRANT SELECT, INSERT, UPDATE, DELETE ON public."audit_logs" TO garments_app;
            """);
    }
}
