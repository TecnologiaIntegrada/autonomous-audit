using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20261004120000_FornecedorUmParaNRecibos")]
public partial class FornecedorUmParaNRecibos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE compras AS c
            SET fornecedor_id = f.id
            FROM fornecedores AS f
            WHERE f.recibo_id IS NOT NULL
              AND f.recibo_id = c.id
              AND c.fornecedor_id IS NULL;

            UPDATE fornecedores
            SET nome = 'Fornecedor nao identificado',
                nome_normalizado = 'FORNECEDOR NAO IDENTIFICADO',
                data_atualizacao = NOW()
            WHERE nome_normalizado IN ('FORNECEDOR NAO IDENTIFICADO', 'FORNECEDOR NAO ENCONTRADO')
               OR (
                    nome_normalizado LIKE '%FORNECEDOR%'
                    AND (
                        nome_normalizado LIKE '%NAO IDENTIFICADO%'
                        OR nome_normalizado LIKE '%NAO ENCONTRADO%'
                    )
               );

            CREATE TEMP TABLE fornecedor_keeper AS
            SELECT DISTINCT ON (usuario_id, nome_normalizado)
                id, usuario_id, nome_normalizado
            FROM fornecedores
            ORDER BY usuario_id, nome_normalizado, data_criacao, id;

            UPDATE compras AS c
            SET fornecedor_id = k.id
            FROM fornecedores AS f
            JOIN fornecedor_keeper AS k
              ON k.usuario_id = f.usuario_id
             AND k.nome_normalizado = f.nome_normalizado
            WHERE c.fornecedor_id = f.id
              AND f.id <> k.id;

            UPDATE fornecedores AS k
            SET
                razao_social = COALESCE(k.razao_social, f.razao_social),
                telefone = COALESCE(k.telefone, f.telefone),
                endereco = COALESCE(k.endereco, f.endereco),
                cpf_cnpj = CASE
                    WHEN k.cpf_cnpj IS NOT NULL THEN k.cpf_cnpj
                    WHEN f.cpf_cnpj IS NULL THEN k.cpf_cnpj
                    WHEN EXISTS (
                        SELECT 1
                        FROM fornecedores AS x
                        WHERE x.usuario_id = k.usuario_id
                          AND x.cpf_cnpj = f.cpf_cnpj
                          AND x.id <> f.id
                    ) THEN k.cpf_cnpj
                    ELSE f.cpf_cnpj
                END,
                data_atualizacao = NOW()
            FROM fornecedores AS f
            WHERE k.id IN (SELECT id FROM fornecedor_keeper)
              AND f.usuario_id = k.usuario_id
              AND f.nome_normalizado = k.nome_normalizado
              AND f.id <> k.id;

            DELETE FROM fornecedores AS f
            USING fornecedor_keeper AS k
            WHERE f.usuario_id = k.usuario_id
              AND f.nome_normalizado = k.nome_normalizado
              AND f.id <> k.id;

            DROP TABLE fornecedor_keeper;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_fornecedores_compras_recibo_id",
            table: "fornecedores");

        migrationBuilder.DropIndex(
            name: "IX_fornecedores_recibo_id",
            table: "fornecedores");

        migrationBuilder.DropColumn(
            name: "recibo_id",
            table: "fornecedores");

        migrationBuilder.DropIndex(
            name: "IX_fornecedores_usuario_id_nome_normalizado",
            table: "fornecedores");

        migrationBuilder.CreateIndex(
            name: "IX_fornecedores_usuario_id_nome_normalizado",
            table: "fornecedores",
            columns: new[] { "usuario_id", "nome_normalizado" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_fornecedores_usuario_id_nome_normalizado",
            table: "fornecedores");

        migrationBuilder.CreateIndex(
            name: "IX_fornecedores_usuario_id_nome_normalizado",
            table: "fornecedores",
            columns: new[] { "usuario_id", "nome_normalizado" });

        migrationBuilder.AddColumn<Guid>(
            name: "recibo_id",
            table: "fornecedores",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_fornecedores_recibo_id",
            table: "fornecedores",
            column: "recibo_id");

        migrationBuilder.AddForeignKey(
            name: "FK_fornecedores_compras_recibo_id",
            table: "fornecedores",
            column: "recibo_id",
            principalTable: "compras",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }
}
