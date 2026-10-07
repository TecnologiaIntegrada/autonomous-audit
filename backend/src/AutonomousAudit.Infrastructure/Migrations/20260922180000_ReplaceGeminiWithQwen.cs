using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260922180000_ReplaceGeminiWithQwen")]
public partial class ReplaceGeminiWithQwen : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "gemini_prompt", newName: "qwen_prompt");
        migrationBuilder.RenameTable(name: "gemini_arquivo", newName: "qwen_arquivo");
        migrationBuilder.RenameTable(name: "usuario_gemini_uso_mensal", newName: "usuario_qwen_uso_mensal");

        migrationBuilder.RenameColumn(name: "gemini_tokens_total", table: "usuarios", newName: "qwen_tokens_total");
        migrationBuilder.RenameColumn(name: "gemini_custo_total_brl", table: "usuarios", newName: "qwen_custo_total_brl");
        migrationBuilder.RenameColumn(name: "gemini_json_bruto", table: "compras", newName: "qwen_json_bruto");

        migrationBuilder.RenameIndex(name: "IX_gemini_prompt_status", table: "qwen_prompt", newName: "IX_qwen_prompt_status");
        migrationBuilder.RenameIndex(name: "IX_gemini_prompt_usuario_id", table: "qwen_prompt", newName: "IX_qwen_prompt_usuario_id");
        migrationBuilder.RenameIndex(name: "IX_gemini_arquivo_status", table: "qwen_arquivo", newName: "IX_qwen_arquivo_status");
        migrationBuilder.RenameIndex(name: "IX_gemini_arquivo_usuario_id", table: "qwen_arquivo", newName: "IX_qwen_arquivo_usuario_id");
        migrationBuilder.RenameIndex(name: "IX_gemini_arquivo_arquivo_recebido_id", table: "qwen_arquivo", newName: "IX_qwen_arquivo_arquivo_recebido_id");
        migrationBuilder.RenameIndex(name: "IX_gemini_arquivo_dropbox_dispatch_id", table: "qwen_arquivo", newName: "IX_qwen_arquivo_dropbox_dispatch_id");
        migrationBuilder.RenameIndex(
            name: "IX_usuario_gemini_uso_mensal_usuario_id_ano_mes",
            table: "usuario_qwen_uso_mensal",
            newName: "IX_usuario_qwen_uso_mensal_usuario_id_ano_mes");

        migrationBuilder.Sql("""ALTER TABLE qwen_prompt RENAME CONSTRAINT "PK_gemini_prompt" TO "PK_qwen_prompt";""");
        migrationBuilder.Sql("""ALTER TABLE qwen_arquivo RENAME CONSTRAINT "PK_gemini_arquivo" TO "PK_qwen_arquivo";""");
        migrationBuilder.Sql("""ALTER TABLE usuario_qwen_uso_mensal RENAME CONSTRAINT "PK_usuario_gemini_uso_mensal" TO "PK_usuario_qwen_uso_mensal";""");
        migrationBuilder.Sql("""ALTER TABLE usuario_qwen_uso_mensal RENAME CONSTRAINT "FK_usuario_gemini_uso_mensal_usuarios_usuario_id" TO "FK_usuario_qwen_uso_mensal_usuarios_usuario_id";""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""ALTER TABLE usuario_qwen_uso_mensal RENAME CONSTRAINT "FK_usuario_qwen_uso_mensal_usuarios_usuario_id" TO "FK_usuario_gemini_uso_mensal_usuarios_usuario_id";""");
        migrationBuilder.Sql("""ALTER TABLE usuario_qwen_uso_mensal RENAME CONSTRAINT "PK_usuario_qwen_uso_mensal" TO "PK_usuario_gemini_uso_mensal";""");
        migrationBuilder.Sql("""ALTER TABLE qwen_arquivo RENAME CONSTRAINT "PK_qwen_arquivo" TO "PK_gemini_arquivo";""");
        migrationBuilder.Sql("""ALTER TABLE qwen_prompt RENAME CONSTRAINT "PK_qwen_prompt" TO "PK_gemini_prompt";""");

        migrationBuilder.RenameIndex(
            name: "IX_usuario_qwen_uso_mensal_usuario_id_ano_mes",
            table: "usuario_qwen_uso_mensal",
            newName: "IX_usuario_gemini_uso_mensal_usuario_id_ano_mes");
        migrationBuilder.RenameIndex(name: "IX_qwen_arquivo_dropbox_dispatch_id", table: "qwen_arquivo", newName: "IX_gemini_arquivo_dropbox_dispatch_id");
        migrationBuilder.RenameIndex(name: "IX_qwen_arquivo_arquivo_recebido_id", table: "qwen_arquivo", newName: "IX_gemini_arquivo_arquivo_recebido_id");
        migrationBuilder.RenameIndex(name: "IX_qwen_arquivo_usuario_id", table: "qwen_arquivo", newName: "IX_gemini_arquivo_usuario_id");
        migrationBuilder.RenameIndex(name: "IX_qwen_arquivo_status", table: "qwen_arquivo", newName: "IX_gemini_arquivo_status");
        migrationBuilder.RenameIndex(name: "IX_qwen_prompt_usuario_id", table: "qwen_prompt", newName: "IX_gemini_prompt_usuario_id");
        migrationBuilder.RenameIndex(name: "IX_qwen_prompt_status", table: "qwen_prompt", newName: "IX_gemini_prompt_status");

        migrationBuilder.RenameColumn(name: "qwen_json_bruto", table: "compras", newName: "gemini_json_bruto");
        migrationBuilder.RenameColumn(name: "qwen_custo_total_brl", table: "usuarios", newName: "gemini_custo_total_brl");
        migrationBuilder.RenameColumn(name: "qwen_tokens_total", table: "usuarios", newName: "gemini_tokens_total");

        migrationBuilder.RenameTable(name: "usuario_qwen_uso_mensal", newName: "usuario_gemini_uso_mensal");
        migrationBuilder.RenameTable(name: "qwen_arquivo", newName: "gemini_arquivo");
        migrationBuilder.RenameTable(name: "qwen_prompt", newName: "gemini_prompt");
    }
}
