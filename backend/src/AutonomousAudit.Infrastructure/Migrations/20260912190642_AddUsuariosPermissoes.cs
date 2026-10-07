using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuariosPermissoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modulos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modulos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email_principal = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    email_secundario = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    telefone_principal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    telefone_secundario = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tipo_docto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    n_docto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_emissao = table.Column<DateOnly>(type: "date", nullable: true),
                    orgao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cidade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    uf = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    pais = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sms_auth = table.Column<bool>(type: "boolean", nullable: false),
                    email_auth = table.Column<bool>(type: "boolean", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recursos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metodo_http = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    rota = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recursos", x => x.id);
                    table.ForeignKey(
                        name: "FK_recursos_modulos_modulo_id",
                        column: x => x.modulo_id,
                        principalTable: "modulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_permissoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_permissoes", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_permissoes_modulos_modulo_id",
                        column: x => x.modulo_id,
                        principalTable: "modulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_permissoes_recursos_recurso_id",
                        column: x => x.recurso_id,
                        principalTable: "recursos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_permissoes_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_modulos_codigo",
                table: "modulos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recursos_chave",
                table: "recursos",
                column: "chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recursos_modulo_id_codigo",
                table: "recursos",
                columns: new[] { "modulo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permissoes_modulo_id",
                table: "usuario_permissoes",
                column: "modulo_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permissoes_recurso_id",
                table: "usuario_permissoes",
                column: "recurso_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permissoes_usuario_id_recurso_id",
                table: "usuario_permissoes",
                columns: new[] { "usuario_id", "recurso_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_email_principal",
                table: "usuarios",
                column: "email_principal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_token",
                table: "usuarios",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_permissoes");

            migrationBuilder.DropTable(
                name: "recursos");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "modulos");
        }
    }
}
