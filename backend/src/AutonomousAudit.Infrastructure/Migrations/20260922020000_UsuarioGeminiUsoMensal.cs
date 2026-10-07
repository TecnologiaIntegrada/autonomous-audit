using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260922020000_UsuarioGeminiUsoMensal")]
    public partial class UsuarioGeminiUsoMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario_gemini_uso_mensal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    tokens = table.Column<long>(type: "bigint", nullable: false),
                    custo_brl = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_gemini_uso_mensal", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_gemini_uso_mensal_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_gemini_uso_mensal_usuario_id_ano_mes",
                table: "usuario_gemini_uso_mensal",
                columns: new[] { "usuario_id", "ano", "mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_gemini_uso_mensal");
        }
    }
}
