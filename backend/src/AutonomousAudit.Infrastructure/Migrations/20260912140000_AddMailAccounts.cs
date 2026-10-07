using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousAudit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMailAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mail_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    server = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    pop_port = table.Column<int>(type: "integer", nullable: true),
                    protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tls_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_accounts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mail_accounts_email",
                table: "mail_accounts",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mail_accounts");
        }
    }
}
