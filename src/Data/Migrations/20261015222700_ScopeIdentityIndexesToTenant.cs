using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScopeIdentityIndexesToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "users");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "roles");

            migrationBuilder.RenameIndex(
                name: "EmailIndex",
                table: "users",
                newName: "IX_users_normalized_email");

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name");

            migrationBuilder.CreateIndex(
                name: "IX_roles_normalized_name",
                table: "roles",
                column: "normalized_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_normalized_user_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_roles_normalized_name",
                table: "roles");

            migrationBuilder.RenameIndex(
                name: "IX_users_normalized_email",
                table: "users",
                newName: "EmailIndex");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);
        }
    }
}
