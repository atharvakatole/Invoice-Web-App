using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesBackend.Migrations
{
    public partial class RestructureBills : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Migrate existing Bills data into BillItems ─────────────────
            // Each old Bill row becomes a Bill header + one BillItem row.

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BillItems"" (
                    ""Id"" uuid NOT NULL,
                    ""BillId"" uuid NOT NULL,
                    ""ItemName"" text NOT NULL DEFAULT 'Item',
                    ""Quantity"" integer NOT NULL DEFAULT 1,
                    ""PricePerItem"" numeric NOT NULL DEFAULT 0,
                    ""IsRefundable"" boolean NOT NULL DEFAULT false,
                    ""ReturnByDate"" timestamp with time zone,
                    ""QuantityReturned"" integer NOT NULL DEFAULT 0,
                    ""QuantityBoughtByClient"" integer NOT NULL DEFAULT 0,
                    ""BoughtByClientName"" text,
                    ""BoughtByClientId"" uuid,
                    ""DraftInvoiceId"" uuid,
                    ""Notes"" text,
                    ""ImageData"" bytea,
                    ""ImageContentType"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_BillItems"" PRIMARY KEY (""Id"")
                );
            ");

            // Migrate existing bill rows: amount → PricePerItem, IsRefundable, ReturnByDate
            // ImageData/ImageContentType columns may or may not exist depending on which
            // migrations ran — use DO block to handle both cases safely.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    img bytea;
                    imgct text;
                    isref boolean;
                    retdate timestamp with time zone;
                    amt numeric;
                    isret boolean;
                BEGIN
                    FOR r IN SELECT * FROM ""Bills"" LOOP
                        -- Safely read optional columns
                        BEGIN img := r.""ImageData""; EXCEPTION WHEN undefined_column THEN img := NULL; END;
                        BEGIN imgct := r.""ImageContentType""; EXCEPTION WHEN undefined_column THEN imgct := NULL; END;
                        BEGIN isref := r.""IsRefundable""; EXCEPTION WHEN undefined_column THEN isref := false; END;
                        BEGIN retdate := r.""ReturnByDate""; EXCEPTION WHEN undefined_column THEN retdate := NULL; END;
                        BEGIN amt := r.""Amount""; EXCEPTION WHEN undefined_column THEN amt := 0; END;
                        BEGIN isret := r.""IsReturned""; EXCEPTION WHEN undefined_column THEN isret := false; END;

                        INSERT INTO ""BillItems"" (
                            ""Id"", ""BillId"", ""ItemName"", ""Quantity"", ""PricePerItem"",
                            ""IsRefundable"", ""ReturnByDate"",
                            ""QuantityReturned"", ""QuantityBoughtByClient"",
                            ""ImageData"", ""ImageContentType"", ""CreatedAt""
                        ) VALUES (
                            gen_random_uuid(), r.""Id"", 'Item', 1, amt,
                            isref, retdate,
                            CASE WHEN isret THEN 1 ELSE 0 END, 0,
                            img, imgct, r.""CreatedAt""
                        );
                    END LOOP;
                END $$;
            ");

            // Drop old columns from Bills that moved to BillItem
            var oldCols = new[] { "Amount", "IsRefundable", "IsReturned", "ReturnByDate",
                                   "ImageData", "ImageContentType" };
            foreach (var col in oldCols)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE ""Bills"" DROP COLUMN IF EXISTS ""{col}"";
                ");
            }

            // Index on BillItems.BillId
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_BillItems_BillId"" ON ""BillItems"" (""BillId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BillItems");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount", table: "Bills", type: "numeric", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<bool>(
                name: "IsRefundable", table: "Bills", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "IsReturned", table: "Bills", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnByDate", table: "Bills", type: "timestamp with time zone", nullable: true);
        }
    }
}
