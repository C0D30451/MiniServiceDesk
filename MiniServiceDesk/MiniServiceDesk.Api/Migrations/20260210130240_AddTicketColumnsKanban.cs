using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniServiceDesk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketColumnsKanban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrderInColumn",
                table: "Tickets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketColumnId",
                table: "Tickets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerUserName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketColumns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketColumnId",
                table: "Tickets",
                column: "TicketColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketColumns_OwnerUserId_Name",
                table: "TicketColumns",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketColumns_TicketColumnId",
                table: "Tickets",
                column: "TicketColumnId",
                principalTable: "TicketColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketColumns_TicketColumnId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketColumns");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TicketColumnId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SortOrderInColumn",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TicketColumnId",
                table: "Tickets");
        }
    }
}
