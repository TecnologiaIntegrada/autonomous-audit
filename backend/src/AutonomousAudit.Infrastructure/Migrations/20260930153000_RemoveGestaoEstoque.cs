using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260930153000_RemoveGestaoEstoque")]
public partial class RemoveGestaoEstoque : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM usuario_permissoes
            WHERE recurso_id IN (SELECT id FROM recursos WHERE chave LIKE 'estoque.%')
               OR modulo_id IN (SELECT id FROM modulos WHERE codigo = 'estoque');
            DELETE FROM recursos
            WHERE chave LIKE 'estoque.%'
               OR modulo_id IN (SELECT id FROM modulos WHERE codigo = 'estoque');
            DELETE FROM modulos WHERE codigo = 'estoque';
            """);

        migrationBuilder.DropTable(name: "estoque_movimentos");

        migrationBuilder.DropIndex(
            name: "IX_compra_itens_status_estoque",
            table: "compra_itens");

        migrationBuilder.DropColumn(
            name: "status_estoque",
            table: "compra_itens");

        migrationBuilder.DropColumn(
            name: "sugestao_conteudo_embalagem",
            table: "compra_itens");

        migrationBuilder.DropColumn(
            name: "sugestao_qtd_convertida",
            table: "compra_itens");

        migrationBuilder.DropColumn(
            name: "sugestao_unidade",
            table: "compra_itens");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status_estoque",
            table: "compra_itens",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "pendente");

        migrationBuilder.AddColumn<decimal>(
            name: "sugestao_conteudo_embalagem",
            table: "compra_itens",
            type: "numeric(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "sugestao_qtd_convertida",
            table: "compra_itens",
            type: "numeric(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "sugestao_unidade",
            table: "compra_itens",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_compra_itens_status_estoque",
            table: "compra_itens",
            column: "status_estoque");

        migrationBuilder.CreateTable(
            name: "estoque_movimentos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                item_id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                produto_id = table.Column<Guid>(type: "uuid", nullable: true),
                qtd_convertida = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                data = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_estoque_movimentos", x => x.id);
                table.ForeignKey(
                    name: "FK_estoque_movimentos_compra_itens_item_id",
                    column: x => x.item_id,
                    principalTable: "compra_itens",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_estoque_movimentos_usuarios_usuario_id",
                    column: x => x.usuario_id,
                    principalTable: "usuarios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_estoque_movimentos_item_id",
            table: "estoque_movimentos",
            column: "item_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_estoque_movimentos_usuario_id",
            table: "estoque_movimentos",
            column: "usuario_id");
    }
}
