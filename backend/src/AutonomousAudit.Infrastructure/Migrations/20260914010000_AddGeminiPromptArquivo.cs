using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiPromptArquivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gemini_prompt",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    caminho_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    max_tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_mensagem_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modelo_resposta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    texto_resposta = table.Column<string>(type: "text", nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gemini_prompt", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gemini_arquivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arquivo_recebido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dropbox_dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    nome_original = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    caminho_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    caminho_dropbox = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    dropbox_inscrito = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    max_tentativas = table.Column<int>(type: "integer", nullable: false),
                    ultima_mensagem_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modelo_resposta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    texto_resposta = table.Column<string>(type: "text", nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gemini_arquivo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gemini_prompt_status",
                table: "gemini_prompt",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_gemini_prompt_usuario_id",
                table: "gemini_prompt",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_gemini_arquivo_arquivo_recebido_id",
                table: "gemini_arquivo",
                column: "arquivo_recebido_id");

            migrationBuilder.CreateIndex(
                name: "IX_gemini_arquivo_dropbox_dispatch_id",
                table: "gemini_arquivo",
                column: "dropbox_dispatch_id");

            migrationBuilder.CreateIndex(
                name: "IX_gemini_arquivo_status",
                table: "gemini_arquivo",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_gemini_arquivo_usuario_id",
                table: "gemini_arquivo",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "gemini_arquivo");
            migrationBuilder.DropTable(name: "gemini_prompt");
        }
    }
}
