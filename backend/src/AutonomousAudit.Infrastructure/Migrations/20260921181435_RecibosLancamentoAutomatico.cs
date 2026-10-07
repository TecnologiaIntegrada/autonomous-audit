using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecibosLancamentoAutomatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recibo_id",
                table: "produtos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recibo_id",
                table: "fornecedores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_produtos_recibo_id",
                table: "produtos",
                column: "recibo_id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_produtos_compras_recibo_id",
                table: "produtos",
                column: "recibo_id",
                principalTable: "compras",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_fornecedores_compras_recibo_id",
                table: "fornecedores");

            migrationBuilder.DropForeignKey(
                name: "FK_produtos_compras_recibo_id",
                table: "produtos");

            migrationBuilder.DropIndex(
                name: "IX_produtos_recibo_id",
                table: "produtos");

            migrationBuilder.DropIndex(
                name: "IX_fornecedores_recibo_id",
                table: "fornecedores");

            migrationBuilder.DropColumn(
                name: "recibo_id",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "recibo_id",
                table: "fornecedores");
        }
    }
}
