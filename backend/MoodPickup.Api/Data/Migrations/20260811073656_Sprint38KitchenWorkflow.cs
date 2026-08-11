using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoodPickup.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint38KitchenWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByEmployeeId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodUsed",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentReceived",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentReceivedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentReceivedByEmployeeId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreparationStartedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparationStartedByEmployeeId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadyAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReadyByEmployeeId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "PaymentReceived" = TRUE
                WHERE "PaymentMethod" = 'Online';

                INSERT INTO "OrderStatusHistory"
                    ("Id", "OrderId", "OldStatus", "NewStatus", "Timestamp",
                     "EmployeeId", "CorrelationId", "Reason")
                SELECT
                    gen_random_uuid(),
                    "Id",
                    NULL,
                    "Status",
                    COALESCE("RejectedAt", "ConfirmedAt", "CreatedAt"),
                    CASE
                        WHEN "Status" = 'Confirmed' THEN "ConfirmedByEmployeeId"
                        WHEN "Status" = 'Rejected' THEN "RejectedByEmployeeId"
                        ELSE NULL
                    END,
                    'migration-sprint-3.8',
                    CASE WHEN "Status" = 'Rejected' THEN "RejectReason" ELSE NULL END
                FROM "Orders";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CompletedByEmployeeId",
                table: "Orders",
                column: "CompletedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentReceivedByEmployeeId",
                table: "Orders",
                column: "PaymentReceivedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PreparationStartedByEmployeeId",
                table: "Orders",
                column: "PreparationStartedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ReadyByEmployeeId",
                table: "Orders",
                column: "ReadyByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_EmployeeId",
                table: "OrderStatusHistory",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_Timestamp",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Employees_CompletedByEmployeeId",
                table: "Orders",
                column: "CompletedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Employees_PaymentReceivedByEmployeeId",
                table: "Orders",
                column: "PaymentReceivedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Employees_PreparationStartedByEmployeeId",
                table: "Orders",
                column: "PreparationStartedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Employees_ReadyByEmployeeId",
                table: "Orders",
                column: "ReadyByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Employees_CompletedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Employees_PaymentReceivedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Employees_PreparationStartedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Employees_ReadyByEmployeeId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CompletedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentReceivedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PreparationStartedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ReadyByEmployeeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CompletedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethodUsed",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentReceived",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentReceivedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentReceivedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreparationStartedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreparationStartedByEmployeeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadyAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadyByEmployeeId",
                table: "Orders");
        }
    }
}
