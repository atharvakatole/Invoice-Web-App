using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesBackend.Migrations
{
    public partial class AddOtpVerification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtpVerifications",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    Email       = table.Column<string>(type: "text", nullable: false),
                    Type        = table.Column<string>(type: "text", nullable: false),
                    CodeHash    = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed      = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AttemptCount= table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_OtpVerifications", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_OtpVerifications_Email_Type",
                table: "OtpVerifications",
                columns: new[] { "Email", "Type" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OtpVerifications");
        }
    }
}
