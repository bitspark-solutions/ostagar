using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBanking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.id);
                    table.CheckConstraint("CK_bank_accounts_identity", "btrim(code) <> '' AND btrim(bank_name) <> '' AND btrim(account_number) <> '' AND currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_bank_accounts_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_bank_accounts_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_accounts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_reconciliations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    closing_balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_reconciliations", x => x.id);
                    table.CheckConstraint("CK_bank_reconciliations_dates", "from_date <= to_date");
                    table.CheckConstraint("CK_bank_reconciliations_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_bank_reconciliations_bank_accounts_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "bank_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_reconciliations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_reconciliation_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gl_line_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_reconciliation_matches", x => x.id);
                    table.CheckConstraint("CK_bank_reconciliation_matches_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_bank_reconciliation_matches_bank_reconciliations_reconcilia~",
                        column: x => x.reconciliation_id,
                        principalTable: "bank_reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_reconciliation_matches_gl_lines_gl_line_id",
                        column: x => x.gl_line_id,
                        principalTable: "gl_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_reconciliation_matches_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_ledger_account_id",
                table: "bank_accounts",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_bank_name_account_number",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "bank_name", "account_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_code",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_tenant_id_ledger_account_id",
                table: "bank_accounts",
                columns: new[] { "tenant_id", "ledger_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_reconciliation_matches_gl_line_id",
                table: "bank_reconciliation_matches",
                column: "gl_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_reconciliation_matches_reconciliation_id",
                table: "bank_reconciliation_matches",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_reconciliation_matches_tenant_id_gl_line_id",
                table: "bank_reconciliation_matches",
                columns: new[] { "tenant_id", "gl_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_reconciliations_bank_account_id",
                table: "bank_reconciliations",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_reconciliations_tenant_id_bank_account_id_to_date",
                table: "bank_reconciliations",
                columns: new[] { "tenant_id", "bank_account_id", "to_date" },
                unique: true);

            migrationBuilder.Sql(BankingEnforcement.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BankingEnforcement.Down);

            migrationBuilder.DropTable(
                name: "bank_reconciliation_matches");

            migrationBuilder.DropTable(
                name: "bank_reconciliations");

            migrationBuilder.DropTable(
                name: "bank_accounts");
        }
    }
}
