using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,9)", nullable: false),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.id);
                    table.CheckConstraint("CK_exchange_rates_rate_positive", "\"rate\" > 0");
                    table.CheckConstraint("CK_exchange_rates_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_exchange_rates_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_resolution",
                table: "exchange_rates",
                columns: new[] { "currency", "tenant_id", "valid_from" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "UQ_exchange_rates_currency_valid_from",
                table: "exchange_rates",
                columns: new[] { "tenant_id", "currency", "valid_from" },
                unique: true);

        // Rate history is protected the same two ways the audit trail is, for the same two reasons.
        //
        // Tenancy first: the assertion script in infra/sql counts tables carrying tenant_id against
        // policies that cover them, so a new tenant-owned table without a policy fails the gate rather
        // than shipping a wider-than-intended read.
        //
        // Then append-only, because a rate the ledger has already used cannot be restated underneath the
        // posting that used it. Editing one changes what a backdated entry computes without touching the
        // entry, which leaves a foreign-currency line and its own source rate disagreeing and nothing in
        // the schema reporting the difference. REVOKE alone is not enough here either, for the same
        // reason as the audit table: this connection owns the table, and ownership outranks a grant.
        migrationBuilder.Sql("""
            ALTER TABLE public."exchange_rates" OWNER TO garments_owner;
            GRANT SELECT, INSERT ON public."exchange_rates" TO garments_app;
            REVOKE UPDATE, DELETE ON public."exchange_rates" FROM garments_app;
            ALTER TABLE public."exchange_rates" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE public."exchange_rates" FORCE ROW LEVEL SECURITY;
            CREATE POLICY "tenant_isolation" ON public."exchange_rates"
                AS PERMISSIVE
                FOR ALL
                TO garments_app
                USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

            CREATE OR REPLACE FUNCTION reject_exchange_rate_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION
                    'Rate history is append-only: % of exchange rate % would restate a rate the ledger may already have posted against. Record a new effective-dated row instead.',
                    lower(TG_OP), OLD.id;
            END
            $$;

            DROP TRIGGER IF EXISTS CK_exchange_rates_append_only ON exchange_rates;
            CREATE TRIGGER CK_exchange_rates_append_only
                BEFORE UPDATE OR DELETE ON exchange_rates
                FOR EACH ROW EXECUTE FUNCTION reject_exchange_rate_mutation();
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS CK_exchange_rates_append_only ON exchange_rates;
                DROP FUNCTION IF EXISTS reject_exchange_rate_mutation();
                DROP POLICY IF EXISTS "tenant_isolation" ON public."exchange_rates";
                """);

            migrationBuilder.DropTable(
                name: "exchange_rates");
        }
    }
}
