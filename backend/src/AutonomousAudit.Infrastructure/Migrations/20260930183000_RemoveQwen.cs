using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260930183000_RemoveQwen")]
public partial class RemoveQwen : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM usuario_permissoes
            WHERE recurso_id IN (SELECT id FROM recursos WHERE chave LIKE 'qwen.%')
               OR modulo_id IN (SELECT id FROM modulos WHERE codigo = 'qwen');
            DELETE FROM recursos
            WHERE chave LIKE 'qwen.%'
               OR modulo_id IN (SELECT id FROM modulos WHERE codigo = 'qwen');
            DELETE FROM modulos WHERE codigo = 'qwen';
            """);

        migrationBuilder.DropTable(name: "qwen_arquivo");
        migrationBuilder.DropTable(name: "qwen_prompt");
        migrationBuilder.DropTable(name: "usuario_qwen_uso_mensal");

        migrationBuilder.DropColumn(name: "qwen_tokens_total", table: "usuarios");
        migrationBuilder.DropColumn(name: "qwen_custo_total_brl", table: "usuarios");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "qwen_tokens_total",
            table: "usuarios",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<decimal>(
            name: "qwen_custo_total_brl",
            table: "usuarios",
            type: "numeric(14,6)",
            precision: 14,
            scale: 6,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "qwen_prompt",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                texto = table.Column<string>(type: "text", nullable: true),
                erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                tentativas = table.Column<int>(type: "integer", nullable: false),
                data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_qwen_prompt", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "qwen_arquivo",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                arquivo_recebido_id = table.Column<Guid>(type: "uuid", nullable: false),
                dropbox_dispatch_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                caminho_dropbox = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                texto = table.Column<string>(type: "text", nullable: true),
                erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                tentativas = table.Column<int>(type: "integer", nullable: false),
                data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_qwen_arquivo", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "usuario_qwen_uso_mensal",
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
                table.PrimaryKey("PK_usuario_qwen_uso_mensal", x => x.id);
            });
    }
}
