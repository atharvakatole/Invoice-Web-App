using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesBackend.Migrations
{
    public partial class AddAssistantFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add UserId and Email to Assistants
            migrationBuilder.AddColumn<Guid>(
                name: "UserId", table: "Assistants",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email", table: "Assistants",
                type: "text", nullable: true);

            // Add Status and AddedByUserId to AssistantAssignments
            migrationBuilder.AddColumn<int>(
                name: "Status", table: "AssistantAssignments",
                type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AddedByUserId", table: "AssistantAssignments",
                type: "uuid", nullable: true);

            // Create ReturnRequests table
            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    Id              = table.Column<Guid>(type: "uuid", nullable: false),
                    BillItemId      = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId    = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityToReturn= table.Column<int>(type: "integer", nullable: false),
                    Notes           = table.Column<string>(type: "text", nullable: true),
                    Status          = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ManagerNotes    = table.Column<string>(type: "text", nullable: true),
                    CreatedAt       = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex("IX_ReturnRequests_BillItemId", "ReturnRequests", "BillItemId");
            migrationBuilder.CreateIndex("IX_ReturnRequests_AssistantUserId", "ReturnRequests", "AssistantUserId");
            migrationBuilder.CreateIndex("IX_Assistants_UserId", "Assistants", "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReturnRequests");
            migrationBuilder.DropColumn(name: "UserId", table: "Assistants");
            migrationBuilder.DropColumn(name: "Email", table: "Assistants");
            migrationBuilder.DropColumn(name: "Status", table: "AssistantAssignments");
            migrationBuilder.DropColumn(name: "AddedByUserId", table: "AssistantAssignments");
        }
    }
}
