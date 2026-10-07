using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260922012800_UsuarioGeminiUso")]
    public partial class UsuarioGeminiUso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "gemini_custo_total_brl",
                table: "usuarios",
                type: "numeric(14,6)",
                precision: 14,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "gemini_tokens_total",
                table: "usuarios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gemini_custo_total_brl",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "gemini_tokens_total",
                table: "usuarios");
        }
    }
}
