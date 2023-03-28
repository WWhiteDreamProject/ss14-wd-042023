using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    public partial class AddYaicaServerNameYaicaSalted : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trait_profile_id",
                table: "trait");

            migrationBuilder.AddColumn<string>(
                name: "server_name",
                table: "server_role_ban",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "server_name",
                table: "server_ban",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trait_profile_id_trait_name",
                table: "trait",
                columns: new[] { "profile_id", "trait_name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trait_profile_id_trait_name",
                table: "trait");

            migrationBuilder.DropColumn(
                name: "server_name",
                table: "server_role_ban");

            migrationBuilder.DropColumn(
                name: "server_name",
                table: "server_ban");

            migrationBuilder.CreateIndex(
                name: "IX_trait_profile_id",
                table: "trait",
                column: "profile_id");
        }
    }
}
