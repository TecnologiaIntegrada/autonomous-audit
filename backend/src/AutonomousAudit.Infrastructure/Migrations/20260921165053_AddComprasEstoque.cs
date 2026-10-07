using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComprasEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    razao_social = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cpf_cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fornecedores", x => x.id);
                    table.ForeignKey(
                        name: "FK_fornecedores_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    marca = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    variante = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    unidade_controle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    conteudo_embalagem = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos", x => x.id);
                    table.ForeignKey(
                        name: "FK_produtos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_compra = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    numero_recibo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    descontos = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    acrescimos = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    forma_pagamento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hash_arquivo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    gemini_json_bruto = table.Column<string>(type: "text", nullable: true),
                    divergencias_json = table.Column<string>(type: "text", nullable: true),
                    chave_duplicidade = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tentativas_processamento = table.Column<int>(type: "integer", nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras", x => x.id);
                    table.ForeignKey(
                        name: "FK_compras_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compras_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produto_nomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produto_nomes", x => x.id);
                    table.ForeignKey(
                        name: "FK_produto_nomes_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "captura_sessoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_captura_sessoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_captura_sessoes_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_anexos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    caminho_local = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_anexos", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_anexos_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dropbox_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    dropbox_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    dropbox_dispatch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    caminho_local_pdf = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_documentos_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_itens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    descricao_original = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    codigo_impresso = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    qtd = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    preco_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    desconto = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sugestao_conteudo_embalagem = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    sugestao_qtd_convertida = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    sugestao_unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status_estoque = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_itens_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compra_itens_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "IX_captura_sessoes_compra_id",
                table: "captura_sessoes",
                column: "compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_captura_sessoes_session_token_hash",
                table: "captura_sessoes",
                column: "session_token_hash",
                unique: true,
                filter: "session_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_captura_sessoes_token_hash",
                table: "captura_sessoes",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compra_anexos_compra_id_ordem",
                table: "compra_anexos",
                columns: new[] { "compra_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_compra_documentos_compra_id",
                table: "compra_documentos",
                column: "compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_itens_compra_id",
                table: "compra_itens",
                column: "compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_itens_produto_id",
                table: "compra_itens",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_itens_status_estoque",
                table: "compra_itens",
                column: "status_estoque");

            migrationBuilder.CreateIndex(
                name: "IX_compras_fornecedor_id",
                table: "compras",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_compras_hash_arquivo",
                table: "compras",
                column: "hash_arquivo");

            migrationBuilder.CreateIndex(
                name: "IX_compras_status",
                table: "compras",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_compras_usuario_id",
                table: "compras",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_compras_usuario_id_chave_duplicidade",
                table: "compras",
                columns: new[] { "usuario_id", "chave_duplicidade" });

            migrationBuilder.CreateIndex(
                name: "IX_estoque_movimentos_item_id",
                table: "estoque_movimentos",
                column: "item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estoque_movimentos_usuario_id",
                table: "estoque_movimentos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_usuario_id_cpf_cnpj",
                table: "fornecedores",
                columns: new[] { "usuario_id", "cpf_cnpj" },
                unique: true,
                filter: "cpf_cnpj IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_fornecedores_usuario_id_nome_normalizado",
                table: "fornecedores",
                columns: new[] { "usuario_id", "nome_normalizado" });

            migrationBuilder.CreateIndex(
                name: "IX_produto_nomes_produto_id_nome_normalizado",
                table: "produto_nomes",
                columns: new[] { "produto_id", "nome_normalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_produtos_usuario_id_nome_normalizado",
                table: "produtos",
                columns: new[] { "usuario_id", "nome_normalizado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "captura_sessoes");

            migrationBuilder.DropTable(
                name: "compra_anexos");

            migrationBuilder.DropTable(
                name: "compra_documentos");

            migrationBuilder.DropTable(
                name: "estoque_movimentos");

            migrationBuilder.DropTable(
                name: "produto_nomes");

            migrationBuilder.DropTable(
                name: "compra_itens");

            migrationBuilder.DropTable(
                name: "compras");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "fornecedores");
        }
    }
}
