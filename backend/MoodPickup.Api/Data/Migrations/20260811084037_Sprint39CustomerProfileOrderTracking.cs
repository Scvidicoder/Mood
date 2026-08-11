using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoodPickup.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint39CustomerProfileOrderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OptionGroupId",
                table: "OrderItemOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OptionValueId",
                table: "OrderItemOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "Customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE "Customers"
                SET "RowVersion" = gen_random_uuid();

                WITH "Matches" AS (
                    SELECT
                        "Snapshot"."Id" AS "SnapshotId",
                        "ProductGroup"."OptionGroupId",
                        "ProductValue"."OptionValueId",
                        COUNT(*) OVER (PARTITION BY "Snapshot"."Id") AS "MatchCount"
                    FROM "OrderItemOptions" AS "Snapshot"
                    INNER JOIN "OrderItems" AS "OrderItem"
                        ON "OrderItem"."Id" = "Snapshot"."OrderItemId"
                    INNER JOIN "ProductOptionGroups" AS "ProductGroup"
                        ON "ProductGroup"."ProductId" = "OrderItem"."ProductId"
                    INNER JOIN "OptionGroups" AS "OptionGroup"
                        ON "OptionGroup"."Id" = "ProductGroup"."OptionGroupId"
                       AND "OptionGroup"."Name" = "Snapshot"."OptionGroupName"
                    INNER JOIN "ProductOptionValues" AS "ProductValue"
                        ON "ProductValue"."ProductOptionGroupId" = "ProductGroup"."Id"
                    INNER JOIN "OptionValues" AS "OptionValue"
                        ON "OptionValue"."Id" = "ProductValue"."OptionValueId"
                       AND "OptionValue"."Name" = "Snapshot"."OptionValueName"
                )
                UPDATE "OrderItemOptions" AS "Snapshot"
                SET
                    "OptionGroupId" = "Matches"."OptionGroupId",
                    "OptionValueId" = "Matches"."OptionValueId"
                FROM "Matches"
                WHERE "Matches"."SnapshotId" = "Snapshot"."Id"
                  AND "Matches"."MatchCount" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionGroupId",
                table: "OrderItemOptions");

            migrationBuilder.DropColumn(
                name: "OptionValueId",
                table: "OrderItemOptions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Customers");
        }
    }
}
