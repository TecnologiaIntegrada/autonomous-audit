using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260921193000_UsuarioVerificacaoETokenRelatorio")]
    public partial class UsuarioVerificacaoETokenRelatorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "email_principal_verificado",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "email_secundario_verificado",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "telefone_principal_verificado",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "telefone_secundario_verificado",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "email_verificacao_codigo",
                table: "usuarios",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_verificacao_canal",
                table: "usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_verificacao_expira",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sms_verificacao_codigo",
                table: "usuarios",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sms_verificacao_canal",
                table: "usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sms_verificacao_expira",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "api_token_jti",
                table: "usuarios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "api_token_expira",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE usuarios
                SET email_principal_verificado = TRUE
                WHERE google_sub IS NOT NULL AND btrim(google_sub) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "email_principal_verificado", table: "usuarios");
            migrationBuilder.DropColumn(name: "email_secundario_verificado", table: "usuarios");
            migrationBuilder.DropColumn(name: "telefone_principal_verificado", table: "usuarios");
            migrationBuilder.DropColumn(name: "telefone_secundario_verificado", table: "usuarios");
            migrationBuilder.DropColumn(name: "email_verificacao_codigo", table: "usuarios");
            migrationBuilder.DropColumn(name: "email_verificacao_canal", table: "usuarios");
            migrationBuilder.DropColumn(name: "email_verificacao_expira", table: "usuarios");
            migrationBuilder.DropColumn(name: "sms_verificacao_codigo", table: "usuarios");
            migrationBuilder.DropColumn(name: "sms_verificacao_canal", table: "usuarios");
            migrationBuilder.DropColumn(name: "sms_verificacao_expira", table: "usuarios");
            migrationBuilder.DropColumn(name: "api_token_jti", table: "usuarios");
            migrationBuilder.DropColumn(name: "api_token_expira", table: "usuarios");
        }
    }
}
