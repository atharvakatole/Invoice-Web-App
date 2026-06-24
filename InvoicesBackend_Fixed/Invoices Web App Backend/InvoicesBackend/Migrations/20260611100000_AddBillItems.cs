using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesBackend.Migrations
{
    public partial class AddBillItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove old bill-level columns (replaced by BillItem-level tracking)
            migrationBuilder.DropColumn(name: "Amount", table: "Bills");
            migrationBuilder.DropColumn(name: "IsReturned", table: "Bills");
            migrationBuilder.DropColumn(name: "ImageData", table: "Bills");
            migrationBuilder.DropColumn(name: "ImageContentType", table: "Bills");

            migrationBuilder.CreateTable(
                name: "BillItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RefundReceived = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0),
                    SoldToClient = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClientSalePrice = table.Column<decimal>(type: "numeric", nullable: true),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImageContentType = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillItems", x => x.Id);
                    table.ForeignKey("FK_BillItems_Bills_BillId", x => x.BillId, "Bills", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_BillItems_BillId", "BillItems", "BillId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("BillItems");
        }
    }
}
