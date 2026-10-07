using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260922220000_AddPerplexityOcrFilas")]
public partial class AddPerplexityOcrFilas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "perplexity_tokens_total",
            table: "usuarios",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<decimal>(
            name: "perplexity_custo_total_brl",
            table: "usuarios",
            type: "numeric(14,6)",
            precision: 14,
            scale: 6,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "perplexity_prompt",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                caminho_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                tentativas = table.Column<int>(type: "integer", nullable: false),
                max_tentativas = table.Column<int>(type: "integer", nullable: false),
                ultima_mensagem_erro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                modelo_resposta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                texto_resposta = table.Column<string>(type: "text", nullable: true),
                data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_perplexity_prompt", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "perplexity_arquivo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                arquivo_recebido_id = table.Column<Guid>(type: "uuid", nullable: false),
                dropbox_dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
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
                modelo_resposta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                texto_resposta = table.Column<string>(type: "text", nullable: true),
                data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_conclusao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_perplexity_arquivo", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "usuario_perplexity_uso_mensal",
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
                table.PrimaryKey("PK_usuario_perplexity_uso_mensal", x => x.id);
                table.ForeignKey(
                    name: "FK_usuario_perplexity_uso_mensal_usuarios_usuario_id",
                    column: x => x.usuario_id,
                    principalTable: "usuarios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_prompt_status",
            table: "perplexity_prompt",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_prompt_usuario_id",
            table: "perplexity_prompt",
            column: "usuario_id");

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_arquivo_arquivo_recebido_id",
            table: "perplexity_arquivo",
            column: "arquivo_recebido_id");

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_arquivo_dropbox_dispatch_id",
            table: "perplexity_arquivo",
            column: "dropbox_dispatch_id");

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_arquivo_status",
            table: "perplexity_arquivo",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_perplexity_arquivo_usuario_id",
            table: "perplexity_arquivo",
            column: "usuario_id");

        migrationBuilder.CreateIndex(
            name: "IX_usuario_perplexity_uso_mensal_usuario_id_ano_mes",
            table: "usuario_perplexity_uso_mensal",
            columns: new[] { "usuario_id", "ano", "mes" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "perplexity_arquivo");
        migrationBuilder.DropTable(name: "perplexity_prompt");
        migrationBuilder.DropTable(name: "usuario_perplexity_uso_mensal");
        migrationBuilder.DropColumn(name: "perplexity_tokens_total", table: "usuarios");
        migrationBuilder.DropColumn(name: "perplexity_custo_total_brl", table: "usuarios");
    }
}
