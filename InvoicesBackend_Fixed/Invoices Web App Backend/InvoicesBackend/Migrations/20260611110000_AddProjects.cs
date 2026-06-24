using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesBackend.Migrations
{
    public partial class AddProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Budget = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            // Add ProjectId to related tables (nullable — existing rows stay valid)
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId", table: "Invoices", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId", table: "Bills", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId", table: "AssistantAssignments", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId", table: "CalendarEvents", type: "uuid", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_BusinessId",
                table: "Projects",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ClientId",
                table: "Projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ProjectId",
                table: "Invoices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ProjectId",
                table: "Bills",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProjectId", table: "Invoices");
            migrationBuilder.DropColumn(name: "ProjectId", table: "Bills");
            migrationBuilder.DropColumn(name: "ProjectId", table: "AssistantAssignments");
            migrationBuilder.DropColumn(name: "ProjectId", table: "CalendarEvents");
            migrationBuilder.DropTable(name: "Projects");
        }
    }
}
