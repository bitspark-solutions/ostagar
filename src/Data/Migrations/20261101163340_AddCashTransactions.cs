using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    cash_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counter_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iteration = table.Column<int>(type: "integer", nullable: false),
                    posted_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_transactions", x => x.id);
                    table.CheckConstraint("CK_cash_transactions_distinct_accounts", "cash_account_id <> counter_account_id");
                    table.CheckConstraint("CK_cash_transactions_positive_amount", "amount > 0");
                    table.CheckConstraint("CK_cash_transactions_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_cash_transactions_accounts_cash_account_id",
                        column: x => x.cash_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_transactions_accounts_counter_account_id",
                        column: x => x.counter_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_transactions_gl_entries_posted_entry_id",
                        column: x => x.posted_entry_id,
                        principalTable: "gl_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_transactions_gl_entries_reversed_by_entry_id",
                        column: x => x.reversed_by_entry_id,
                        principalTable: "gl_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_transactions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_transactions_cash_account_id",
                table: "cash_transactions",
                column: "cash_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_transactions_counter_account_id",
                table: "cash_transactions",
                column: "counter_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_transactions_posted_entry_id",
                table: "cash_transactions",
                column: "posted_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_transactions_reversed_by_entry_id",
                table: "cash_transactions",
                column: "reversed_by_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_transactions_tenant_id_transaction_date",
                table: "cash_transactions",
                columns: new[] { "tenant_id", "transaction_date" });

            // No DELETE grant: a recorded cash movement is corrected by reversal, never erased. The
            // document is mutable (status, entry links) so UPDATE stays, unlike the ledger tables it
            // writes to.
            migrationBuilder.Sql("""
                ALTER TABLE public."cash_transactions" OWNER TO garments_owner;
                GRANT SELECT, INSERT, UPDATE ON public."cash_transactions" TO garments_app;
                ALTER TABLE public."cash_transactions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."cash_transactions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY "tenant_isolation" ON public."cash_transactions"
                    AS PERMISSIVE
                    FOR ALL
                    TO garments_app
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS "tenant_isolation" ON public."cash_transactions";
                ALTER TABLE public."cash_transactions" NO FORCE ROW LEVEL SECURITY;
                REVOKE ALL ON public."cash_transactions" FROM garments_app;
                """);

            migrationBuilder.DropTable(
                name: "cash_transactions");
        }
    }
}
