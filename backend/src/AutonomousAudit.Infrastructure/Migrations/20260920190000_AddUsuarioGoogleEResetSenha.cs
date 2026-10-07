using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260920190000_AddUsuarioGoogleEResetSenha")]
    public partial class AddUsuarioGoogleEResetSenha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_sub",
                table: "usuarios",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_picture",
                table: "usuarios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "senha_reset_codigo",
                table: "usuarios",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "senha_reset_expira",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_google_sub",
                table: "usuarios",
                column: "google_sub",
                unique: true,
                filter: "google_sub IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_google_sub",
                table: "usuarios");

            migrationBuilder.DropColumn(name: "google_sub", table: "usuarios");
            migrationBuilder.DropColumn(name: "google_picture", table: "usuarios");
            migrationBuilder.DropColumn(name: "senha_reset_codigo", table: "usuarios");
            migrationBuilder.DropColumn(name: "senha_reset_expira", table: "usuarios");
        }
    }
}
