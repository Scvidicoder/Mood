using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoodPickup.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint36CheckoutOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderDailySequences",
                columns: table => new
                {
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDailySequences", x => x.OrderDate);
                    table.CheckConstraint("CK_OrderDailySequences_LastValue_Positive", "\"LastValue\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PickupMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedPickupTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerPhoneNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_DiscountTotal_NonNegative", "\"DiscountTotal\" >= 0");
                    table.CheckConstraint("CK_Orders_ScheduledPickupRequiresTime", "(\"PickupMode\" = 'AsSoonAsPossible' AND \"RequestedPickupTime\" IS NULL) OR (\"PickupMode\" = 'Scheduled' AND \"RequestedPickupTime\" IS NOT NULL)");
                    table.CheckConstraint("CK_Orders_Subtotal_NonNegative", "\"Subtotal\" >= 0");
                    table.CheckConstraint("CK_Orders_Total_Matches_Subtotal_Discount", "\"Total\" = \"Subtotal\" - \"DiscountTotal\"");
                    table.CheckConstraint("CK_Orders_Total_NonNegative", "\"Total\" >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsAvailableAtPurchase = table.Column<bool>(type: "boolean", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    VolumeMilliliters = table.Column<int>(type: "integer", nullable: true),
                    WeightGrams = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_BasePrice_NonNegative", "\"BasePrice\" >= 0");
                    table.CheckConstraint("CK_OrderItems_Calories_NonNegative", "\"Calories\" IS NULL OR \"Calories\" >= 0");
                    table.CheckConstraint("CK_OrderItems_FinalPrice_NonNegative", "\"FinalPrice\" >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity_Range", "\"Quantity\" >= 1 AND \"Quantity\" <= 99");
                    table.CheckConstraint("CK_OrderItems_Volume_NonNegative", "\"VolumeMilliliters\" IS NULL OR \"VolumeMilliliters\" >= 0");
                    table.CheckConstraint("CK_OrderItems_Weight_NonNegative", "\"WeightGrams\" IS NULL OR \"WeightGrams\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionGroupName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OptionValueName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PriceModifier = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CaloriesModifier = table.Column<int>(type: "integer", nullable: true),
                    VolumeModifier = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemOptions", x => x.Id);
                    table.CheckConstraint("CK_OrderItemOptions_CaloriesModifier_NonNegative", "\"CaloriesModifier\" IS NULL OR \"CaloriesModifier\" >= 0");
                    table.CheckConstraint("CK_OrderItemOptions_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_OrderItemOptions_PriceModifier_NonNegative", "\"PriceModifier\" >= 0");
                    table.CheckConstraint("CK_OrderItemOptions_VolumeModifier_NonNegative", "\"VolumeModifier\" IS NULL OR \"VolumeModifier\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItemOptions_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemOptions_OrderItemId_DisplayOrder",
                table: "OrderItemOptions",
                columns: new[] { "OrderItemId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDailySequences");

            migrationBuilder.DropTable(
                name: "OrderItemOptions");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
