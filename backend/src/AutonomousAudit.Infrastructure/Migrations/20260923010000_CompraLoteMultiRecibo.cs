using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AutonomousAudit.Infrastructure;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations;

[DbContext(typeof(AuditDbContext))]
[Migration("20260923010000_CompraLoteMultiRecibo")]
public partial class CompraLoteMultiRecibo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "recibo_origem_id",
            table: "compras",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "paginas",
            table: "compras",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_compras_recibo_origem_id",
            table: "compras",
            column: "recibo_origem_id");

        migrationBuilder.AddForeignKey(
            name: "FK_compras_compras_recibo_origem_id",
            table: "compras",
            column: "recibo_origem_id",
            principalTable: "compras",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_compras_compras_recibo_origem_id", table: "compras");
        migrationBuilder.DropIndex(name: "IX_compras_recibo_origem_id", table: "compras");
        migrationBuilder.DropColumn(name: "recibo_origem_id", table: "compras");
        migrationBuilder.DropColumn(name: "paginas", table: "compras");
    }
}
