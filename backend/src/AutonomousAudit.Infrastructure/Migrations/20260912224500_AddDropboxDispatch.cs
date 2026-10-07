using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDropboxDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dropbox_dispatch",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arquivo_recebido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    caminho_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    caminho_dropbox = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    dropbox_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    max_tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_mensagem_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dropbox_dispatch", x => x.id);
                    table.ForeignKey(
                        name: "FK_dropbox_dispatch_arquivos_recebidos_arquivo_recebido_id",
                        column: x => x.arquivo_recebido_id,
                        principalTable: "arquivos_recebidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dropbox_dispatch_arquivo_recebido_id",
                table: "dropbox_dispatch",
                column: "arquivo_recebido_id");

            migrationBuilder.CreateIndex(
                name: "IX_dropbox_dispatch_status",
                table: "dropbox_dispatch",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "dropbox_dispatch");
        }
    }
}
