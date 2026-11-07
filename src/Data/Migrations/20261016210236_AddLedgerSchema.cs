using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_bangla = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sub_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    is_postable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.CheckConstraint("CK_accounts_postable_is_leaf", "\"is_postable\" OR \"parent_id\" IS NULL");
                    table.CheckConstraint("CK_accounts_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_accounts_accounts_parent_id",
                        column: x => x.parent_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    soft_locked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    hard_locked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    tax_locked_on = table.Column<DateOnly>(type: "date", nullable: true),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_bangla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_periods", x => x.id);
                    table.CheckConstraint("CK_fiscal_periods_number_in_year", "\"number\" BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_fiscal_periods_ordered", "\"end_date\" >= \"start_date\"");
                    table.CheckConstraint("CK_fiscal_periods_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_fiscal_periods_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_bangla = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    number_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    default_debit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_credit_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allows_drafts = table.Column<bool>(type: "boolean", nullable: false),
                    uses_storno = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    next_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journals", x => x.id);
                    table.UniqueConstraint("AK_journals_tenant_id_code", x => new { x.tenant_id, x.code });
                    table.CheckConstraint("CK_journals_next_number", "\"next_number\" >= 1");
                    table.CheckConstraint("CK_journals_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_journals_accounts_default_credit_account_id",
                        column: x => x.default_credit_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journals_accounts_default_debit_account_id",
                        column: x => x.default_debit_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journals_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gl_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    journal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    memo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iteration = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uses_storno = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_entries", x => x.id);
                    table.CheckConstraint("CK_gl_entries_iteration", "\"iteration\" >= 1");
                    table.CheckConstraint("CK_gl_entries_reversal_not_self", "\"reverses_entry_id\" IS NULL OR \"reverses_entry_id\" <> \"id\"");
                    table.CheckConstraint("CK_gl_entries_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_gl_entries_fiscal_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "fiscal_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_entries_gl_entries_reverses_entry_id",
                        column: x => x.reverses_entry_id,
                        principalTable: "gl_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_entries_journals_tenant_id_journal_code",
                        columns: x => new { x.tenant_id, x.journal_code },
                        principalTable: "journals",
                        principalColumns: new[] { "tenant_id", "code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_entries_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gl_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_order = table.Column<int>(type: "integer", nullable: false),
                    debit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    credit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_in_currency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,9)", nullable: false),
                    exchange_rate_date = table.Column<DateOnly>(type: "date", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    rule_version_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gl_lines", x => x.id);
                    table.CheckConstraint("CK_gl_line_currency_amount", "\"amount_in_currency\" <> 0");
                    table.CheckConstraint("CK_gl_line_debit_xor_credit", "(\"debit\" > 0 AND \"credit\" IS NULL) OR (\"credit\" > 0 AND \"debit\" IS NULL)");
                    table.CheckConstraint("CK_gl_line_rate_positive", "\"exchange_rate\" > 0");
                    table.CheckConstraint("CK_gl_lines_tenant_scope", "tenant_id IS NOT NULL AND tenant_id = current_setting('app.tenant_id', true)::uuid");
                    table.ForeignKey(
                        name: "FK_gl_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gl_lines_gl_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "gl_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gl_lines_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_parent_id",
                table: "accounts",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_path",
                table: "accounts",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_tenant_id_code",
                table: "accounts",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_tenant_id_parent_id",
                table: "accounts",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_periods_tenant_id_fiscal_year_number",
                table: "fiscal_periods",
                columns: new[] { "tenant_id", "fiscal_year", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gl_entries_period_id",
                table: "gl_entries",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_entries_reverses_entry_id",
                table: "gl_entries",
                column: "reverses_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_gl_entries_tenant_id_posting_date",
                table: "gl_entries",
                columns: new[] { "tenant_id", "posting_date" });

            migrationBuilder.CreateIndex(
                name: "UQ_gl_entries_number",
                table: "gl_entries",
                columns: new[] { "tenant_id", "journal_code", "period_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_gl_entries_source_key",
                table: "gl_entries",
                columns: new[] { "tenant_id", "source_type", "source_id", "iteration" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gl_lines_account_id_tenant_id",
                table: "gl_lines",
                columns: new[] { "account_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_gl_lines_entry_id_line_order",
                table: "gl_lines",
                columns: new[] { "entry_id", "line_order" });

            migrationBuilder.CreateIndex(
                name: "IX_gl_lines_tenant_id_account_id_entry_id",
                table: "gl_lines",
                columns: new[] { "tenant_id", "account_id", "entry_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journals_default_credit_account_id",
                table: "journals",
                column: "default_credit_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_journals_default_debit_account_id",
                table: "journals",
                column: "default_debit_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_journals_tenant_id_code",
                table: "journals",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_role_grants_tenants_tenant_id",
                table: "role_grants",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_role_grants_tenants_tenant_id",
                table: "role_grants");

            migrationBuilder.DropTable(
                name: "gl_lines");

            migrationBuilder.DropTable(
                name: "gl_entries");

            migrationBuilder.DropTable(
                name: "fiscal_periods");

            migrationBuilder.DropTable(
                name: "journals");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:ltree", ",,");
        }
    }
}
