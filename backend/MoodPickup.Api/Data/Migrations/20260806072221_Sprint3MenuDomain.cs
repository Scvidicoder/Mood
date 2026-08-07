using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoodPickup.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3MenuDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_Categories_Name_Trimmed", "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.CheckConstraint("CK_MediaFiles_FileSizeBytes_NonNegative", "\"FileSizeBytes\" >= 0");
                    table.CheckConstraint("CK_MediaFiles_Height_Positive", "\"Height\" IS NULL OR \"Height\" > 0");
                    table.CheckConstraint("CK_MediaFiles_Width_Positive", "\"Width\" IS NULL OR \"Width\" > 0");
                    table.ForeignKey(
                        name: "FK_MediaFiles_Employees_CreatedByEmployeeId",
                        column: x => x.CreatedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SelectionType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DefaultIsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultMinimumSelections = table.Column<int>(type: "integer", nullable: false),
                    DefaultMaximumSelections = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroups", x => x.Id);
                    table.CheckConstraint("CK_OptionGroups_DefaultMaximumSelections_Positive", "\"DefaultMaximumSelections\" IS NULL OR \"DefaultMaximumSelections\" >= 1");
                    table.CheckConstraint("CK_OptionGroups_DefaultMinimumSelections_NonNegative", "\"DefaultMinimumSelections\" >= 0");
                    table.CheckConstraint("CK_OptionGroups_DefaultSelectionRange", "\"DefaultMaximumSelections\" IS NULL OR \"DefaultMinimumSelections\" <= \"DefaultMaximumSelections\"");
                    table.CheckConstraint("CK_OptionGroups_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_OptionGroups_Name_Trimmed", "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                    table.CheckConstraint("CK_OptionGroups_RequiredMinimum", "NOT \"DefaultIsRequired\" OR \"DefaultMinimumSelections\" >= 1");
                    table.CheckConstraint("CK_OptionGroups_SingleMaximum", "\"SelectionType\" <> 'Single' OR \"DefaultMaximumSelections\" IS NULL OR \"DefaultMaximumSelections\" <= 1");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Ingredients = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DefaultWeightGrams = table.Column<int>(type: "integer", nullable: true),
                    DefaultVolumeMilliliters = table.Column<int>(type: "integer", nullable: true),
                    DefaultCalories = table.Column<int>(type: "integer", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_BasePrice_NonNegative", "\"BasePrice\" >= 0");
                    table.CheckConstraint("CK_Products_DefaultCalories_NonNegative", "\"DefaultCalories\" IS NULL OR \"DefaultCalories\" >= 0");
                    table.CheckConstraint("CK_Products_DefaultVolumeMilliliters_NonNegative", "\"DefaultVolumeMilliliters\" IS NULL OR \"DefaultVolumeMilliliters\" >= 0");
                    table.CheckConstraint("CK_Products_DefaultWeightGrams_NonNegative", "\"DefaultWeightGrams\" IS NULL OR \"DefaultWeightGrams\" >= 0");
                    table.CheckConstraint("CK_Products_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_Products_Name_Trimmed", "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_MediaFiles_ImageId",
                        column: x => x.ImageId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OptionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionValues", x => x.Id);
                    table.CheckConstraint("CK_OptionValues_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_OptionValues_Name_Trimmed", "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                    table.ForeignKey(
                        name: "FK_OptionValues_OptionGroups_OptionGroupId",
                        column: x => x.OptionGroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumSelections = table.Column<int>(type: "integer", nullable: false),
                    MaximumSelections = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionGroups", x => x.Id);
                    table.CheckConstraint("CK_ProductOptionGroups_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ProductOptionGroups_MaximumSelections_Positive", "\"MaximumSelections\" >= 1");
                    table.CheckConstraint("CK_ProductOptionGroups_MinimumSelections_NonNegative", "\"MinimumSelections\" >= 0");
                    table.CheckConstraint("CK_ProductOptionGroups_RequiredMinimum", "NOT \"IsRequired\" OR \"MinimumSelections\" >= 1");
                    table.CheckConstraint("CK_ProductOptionGroups_SelectionRange", "\"MinimumSelections\" <= \"MaximumSelections\"");
                    table.ForeignKey(
                        name: "FK_ProductOptionGroups_OptionGroups_OptionGroupId",
                        column: x => x.OptionGroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductOptionGroups_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductOptionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceModifier = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    VolumeMilliliters = table.Column<int>(type: "integer", nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionValues", x => x.Id);
                    table.CheckConstraint("CK_ProductOptionValues_Calories_NonNegative", "\"Calories\" IS NULL OR \"Calories\" >= 0");
                    table.CheckConstraint("CK_ProductOptionValues_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ProductOptionValues_PriceModifier_NonNegative", "\"PriceModifier\" >= 0");
                    table.CheckConstraint("CK_ProductOptionValues_VolumeMilliliters_NonNegative", "\"VolumeMilliliters\" IS NULL OR \"VolumeMilliliters\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductOptionValues_OptionValues_OptionValueId",
                        column: x => x.OptionValueId,
                        principalTable: "OptionValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductOptionValues_ProductOptionGroups_ProductOptionGroupId",
                        column: x => x.ProductOptionGroupId,
                        principalTable: "ProductOptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsDeleted_IsVisible_DisplayOrder",
                table: "Categories",
                columns: new[] { "IsDeleted", "IsVisible", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_CreatedByEmployeeId",
                table: "MediaFiles",
                column: "CreatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_IsDeleted_CreatedAt",
                table: "MediaFiles",
                columns: new[] { "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_StorageProvider_StorageKey",
                table: "MediaFiles",
                columns: new[] { "StorageProvider", "StorageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroups_IsDeleted_IsActive_DisplayOrder",
                table: "OptionGroups",
                columns: new[] { "IsDeleted", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroups_NormalizedName",
                table: "OptionGroups",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_OptionValues_OptionGroupId_IsDeleted_IsActive_DisplayOrder",
                table: "OptionValues",
                columns: new[] { "OptionGroupId", "IsDeleted", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionValues_OptionGroupId_NormalizedName",
                table: "OptionValues",
                columns: new[] { "OptionGroupId", "NormalizedName" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_OptionGroupId",
                table: "ProductOptionGroups",
                column: "OptionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_IsActive_DisplayOrder",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_OptionGroupId",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "OptionGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionValues_OptionValueId",
                table: "ProductOptionValues",
                column: "OptionValueId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionValues_ProductOptionGroupId_IsAvailable_Displa~",
                table: "ProductOptionValues",
                columns: new[] { "ProductOptionGroupId", "IsAvailable", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionValues_ProductOptionGroupId_OptionValueId",
                table: "ProductOptionValues",
                columns: new[] { "ProductOptionGroupId", "OptionValueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_IsDeleted_IsVisible_DisplayOrder",
                table: "Products",
                columns: new[] { "CategoryId", "IsDeleted", "IsVisible", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ImageId",
                table: "Products",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_IsAvailable",
                table: "Products",
                columns: new[] { "IsDeleted", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedName",
                table: "Products",
                column: "NormalizedName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductOptionValues");

            migrationBuilder.DropTable(
                name: "OptionValues");

            migrationBuilder.DropTable(
                name: "ProductOptionGroups");

            migrationBuilder.DropTable(
                name: "OptionGroups");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "MediaFiles");
        }
    }
}
