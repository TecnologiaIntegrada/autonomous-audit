using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioCep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cep",
                table: "usuarios",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cep",
                table: "usuarios");
        }
    }
}
