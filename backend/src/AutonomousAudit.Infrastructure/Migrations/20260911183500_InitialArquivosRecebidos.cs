using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialArquivosRecebidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arquivos_recebidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tamanho_mb = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    data_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usuario_remetente = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arquivos_recebidos", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arquivos_recebidos");
        }
    }
}
