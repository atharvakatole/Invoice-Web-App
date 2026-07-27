using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace InvoicesBackend.Migrations
{
    public partial class AddPersonalExpenses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonalExpenses",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId  = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category    = table.Column<string>(type: "text", nullable: false, defaultValue: "Other"),
                    Amount      = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes       = table.Column<string>(type: "text", nullable: true),
                    ProjectId   = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PersonalExpenses", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PersonalExpenses_BusinessId",
                table: "PersonalExpenses",
                column: "BusinessId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PersonalExpenses");
        }
    }
}
