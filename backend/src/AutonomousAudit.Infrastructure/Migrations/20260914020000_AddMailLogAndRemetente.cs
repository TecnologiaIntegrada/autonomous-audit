using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailLogAndRemetente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "remetente",
                table: "mail_accounts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE mail_accounts
                SET remetente = email
                WHERE remetente IS NULL OR btrim(remetente) = '';
                """);

            migrationBuilder.CreateTable(
                name: "mail_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    de = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    para = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    assunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nome_anexo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    max_tentativas = table.Column<int>(type: "integer", nullable: false),
                    resposta_servidor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ultima_mensagem_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_atualizacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mail_log_conta_id",
                table: "mail_log",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "IX_mail_log_status",
                table: "mail_log",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_mail_log_usuario_id",
                table: "mail_log",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mail_log");

            migrationBuilder.DropColumn(
                name: "remetente",
                table: "mail_accounts");
        }
    }
}
