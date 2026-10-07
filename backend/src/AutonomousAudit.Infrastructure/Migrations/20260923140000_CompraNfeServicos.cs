using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260923140000_CompraNfeServicos")]
public partial class CompraNfeServicos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "tipo_documento",
            table: "compras",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "recibo");

        migrationBuilder.AddColumn<string>(
            name: "tipo_item",
            table: "compras",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "produto");

        migrationBuilder.AddColumn<int>(name: "danfe_tipo", table: "compras", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "serie", table: "compras", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "folha", table: "compras", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "chave_acesso", table: "compras", type: "character varying(60)", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "codigo_barras", table: "compras", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "protocolo_autorizacao", table: "compras", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "protocolo_data", table: "compras", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "natureza_operacao", table: "compras", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>(name: "inscricao_estadual", table: "compras", type: "character varying(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "inscricao_estadual_st", table: "compras", type: "character varying(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "data_emissao", table: "compras", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_compras_chave_acesso", table: "compras", column: "chave_acesso");
        migrationBuilder.CreateIndex(name: "IX_compras_tipo_documento", table: "compras", column: "tipo_documento");

        migrationBuilder.AddColumn<string>(name: "codigo_externo", table: "produtos", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ncm_sh", table: "produtos", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "csosn", table: "produtos", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "cfop", table: "produtos", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_unitario", table: "produtos", type: "numeric(18,4)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_desconto", table: "produtos", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_liquido", table: "produtos", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "base_icms", table: "produtos", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_icms", table: "produtos", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_ipi", table: "produtos", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "aliq_icms", table: "produtos", type: "numeric(8,4)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "aliq_ipi", table: "produtos", type: "numeric(8,4)", nullable: true);

        migrationBuilder.AddColumn<Guid>(name: "servico_id", table: "compra_itens", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "tipo_item",
            table: "compra_itens",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "produto");
        migrationBuilder.AddColumn<string>(name: "ncm_sh", table: "compra_itens", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "csosn", table: "compra_itens", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "cfop", table: "compra_itens", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_liquido", table: "compra_itens", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "base_icms", table: "compra_itens", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_icms", table: "compra_itens", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "valor_ipi", table: "compra_itens", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "aliq_icms", table: "compra_itens", type: "numeric(8,4)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "aliq_ipi", table: "compra_itens", type: "numeric(8,4)", nullable: true);

        migrationBuilder.CreateTable(
            name: "servicos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                recibo_id = table.Column<Guid>(type: "uuid", nullable: true),
                nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                nome_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                codigo_externo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                unidade_controle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ncm_sh = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                csosn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                cfop = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                valor_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                valor_desconto = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_liquido = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                base_icms = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_icms = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_ipi = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                aliq_icms = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                aliq_ipi = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_servicos", x => x.id);
                table.ForeignKey(
                    name: "FK_servicos_usuarios_usuario_id",
                    column: x => x.usuario_id,
                    principalTable: "usuarios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_servicos_compras_recibo_id",
                    column: x => x.recibo_id,
                    principalTable: "compras",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(name: "IX_servicos_usuario_id_nome_normalizado", table: "servicos", columns: new[] { "usuario_id", "nome_normalizado" });
        migrationBuilder.CreateIndex(name: "IX_servicos_recibo_id", table: "servicos", column: "recibo_id");

        migrationBuilder.CreateIndex(name: "IX_compra_itens_servico_id", table: "compra_itens", column: "servico_id");
        migrationBuilder.AddForeignKey(
            name: "FK_compra_itens_servicos_servico_id",
            table: "compra_itens",
            column: "servico_id",
            principalTable: "servicos",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.CreateTable(
            name: "compra_nfe_destinatarios",
            columns: table => new
            {
                compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                nome_razao_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                cpf_cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                cep = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                inscricao_estadual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                data_emissao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                data_saida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                hora_saida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_compra_nfe_destinatarios", x => x.compra_id);
                table.ForeignKey(
                    name: "FK_compra_nfe_destinatarios_compras_compra_id",
                    column: x => x.compra_id,
                    principalTable: "compras",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "compra_nfe_impostos",
            columns: table => new
            {
                compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                base_icms = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_icms = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                base_icms_st = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_icms_st = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_total_produtos = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_frete = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_seguro = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                desconto = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                outras_despesas = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_ipi = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                valor_total_nota = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_compra_nfe_impostos", x => x.compra_id);
                table.ForeignKey(
                    name: "FK_compra_nfe_impostos_compras_compra_id",
                    column: x => x.compra_id,
                    principalTable: "compras",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "compra_nfe_transportadores",
            columns: table => new
            {
                compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                nome_razao_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                frete_por_conta = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                codigo_antt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                placa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                cpf_cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                uf_endereco = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                inscricao_estadual = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                quantidade_volumes = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                especie = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                marca = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                numeracao = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                peso_bruto = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                peso_liquido = table.Column<decimal>(type: "numeric(18,4)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_compra_nfe_transportadores", x => x.compra_id);
                table.ForeignKey(
                    name: "FK_compra_nfe_transportadores_compras_compra_id",
                    column: x => x.compra_id,
                    principalTable: "compras",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "compra_nfe_adicionais",
            columns: table => new
            {
                compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                informacoes_complementares = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                reservado_ao_fisco = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                data_hora_impressao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_compra_nfe_adicionais", x => x.compra_id);
                table.ForeignKey(
                    name: "FK_compra_nfe_adicionais_compras_compra_id",
                    column: x => x.compra_id,
                    principalTable: "compras",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "compra_nfe_destinatarios");
        migrationBuilder.DropTable(name: "compra_nfe_impostos");
        migrationBuilder.DropTable(name: "compra_nfe_transportadores");
        migrationBuilder.DropTable(name: "compra_nfe_adicionais");
        migrationBuilder.DropForeignKey(name: "FK_compra_itens_servicos_servico_id", table: "compra_itens");
        migrationBuilder.DropIndex(name: "IX_compra_itens_servico_id", table: "compra_itens");
        migrationBuilder.DropTable(name: "servicos");
        migrationBuilder.DropColumn(name: "servico_id", table: "compra_itens");
        migrationBuilder.DropColumn(name: "tipo_item", table: "compra_itens");
        migrationBuilder.DropColumn(name: "ncm_sh", table: "compra_itens");
        migrationBuilder.DropColumn(name: "csosn", table: "compra_itens");
        migrationBuilder.DropColumn(name: "cfop", table: "compra_itens");
        migrationBuilder.DropColumn(name: "valor_liquido", table: "compra_itens");
        migrationBuilder.DropColumn(name: "base_icms", table: "compra_itens");
        migrationBuilder.DropColumn(name: "valor_icms", table: "compra_itens");
        migrationBuilder.DropColumn(name: "valor_ipi", table: "compra_itens");
        migrationBuilder.DropColumn(name: "aliq_icms", table: "compra_itens");
        migrationBuilder.DropColumn(name: "aliq_ipi", table: "compra_itens");
        migrationBuilder.DropColumn(name: "codigo_externo", table: "produtos");
        migrationBuilder.DropColumn(name: "ncm_sh", table: "produtos");
        migrationBuilder.DropColumn(name: "csosn", table: "produtos");
        migrationBuilder.DropColumn(name: "cfop", table: "produtos");
        migrationBuilder.DropColumn(name: "valor_unitario", table: "produtos");
        migrationBuilder.DropColumn(name: "valor_desconto", table: "produtos");
        migrationBuilder.DropColumn(name: "valor_liquido", table: "produtos");
        migrationBuilder.DropColumn(name: "base_icms", table: "produtos");
        migrationBuilder.DropColumn(name: "valor_icms", table: "produtos");
        migrationBuilder.DropColumn(name: "valor_ipi", table: "produtos");
        migrationBuilder.DropColumn(name: "aliq_icms", table: "produtos");
        migrationBuilder.DropColumn(name: "aliq_ipi", table: "produtos");
        migrationBuilder.DropIndex(name: "IX_compras_chave_acesso", table: "compras");
        migrationBuilder.DropIndex(name: "IX_compras_tipo_documento", table: "compras");
        migrationBuilder.DropColumn(name: "tipo_documento", table: "compras");
        migrationBuilder.DropColumn(name: "tipo_item", table: "compras");
        migrationBuilder.DropColumn(name: "danfe_tipo", table: "compras");
        migrationBuilder.DropColumn(name: "serie", table: "compras");
        migrationBuilder.DropColumn(name: "folha", table: "compras");
        migrationBuilder.DropColumn(name: "chave_acesso", table: "compras");
        migrationBuilder.DropColumn(name: "codigo_barras", table: "compras");
        migrationBuilder.DropColumn(name: "protocolo_autorizacao", table: "compras");
        migrationBuilder.DropColumn(name: "protocolo_data", table: "compras");
        migrationBuilder.DropColumn(name: "natureza_operacao", table: "compras");
        migrationBuilder.DropColumn(name: "inscricao_estadual", table: "compras");
        migrationBuilder.DropColumn(name: "inscricao_estadual_st", table: "compras");
        migrationBuilder.DropColumn(name: "data_emissao", table: "compras");
    }
}
