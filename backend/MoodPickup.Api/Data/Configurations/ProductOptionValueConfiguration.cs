using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class ProductOptionValueConfiguration
    : IEntityTypeConfiguration<ProductOptionValue>
{
    public void Configure(EntityTypeBuilder<ProductOptionValue> builder)
    {
        builder.ToTable(
            "ProductOptionValues",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ProductOptionValues_PriceModifier_NonNegative",
                    "\"PriceModifier\" >= 0");
                table.HasCheckConstraint(
                    "CK_ProductOptionValues_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
                table.HasCheckConstraint(
                    "CK_ProductOptionValues_VolumeMilliliters_NonNegative",
                    "\"VolumeMilliliters\" IS NULL OR \"VolumeMilliliters\" >= 0");
                table.HasCheckConstraint(
                    "CK_ProductOptionValues_Calories_NonNegative",
                    "\"Calories\" IS NULL OR \"Calories\" >= 0");
            });
        builder.HasKey(productOptionValue => productOptionValue.Id);
        builder.Property(productOptionValue => productOptionValue.PriceModifier).HasPrecision(12, 2);
        builder.Property(productOptionValue => productOptionValue.RowVersion).IsConcurrencyToken();
        builder
            .HasOne(productOptionValue => productOptionValue.ProductOptionGroup)
            .WithMany(productOptionGroup => productOptionGroup.Values)
            .HasForeignKey(productOptionValue => productOptionValue.ProductOptionGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(productOptionValue => productOptionValue.OptionValue)
            .WithMany(optionValue => optionValue.ProductAssignments)
            .HasForeignKey(productOptionValue => productOptionValue.OptionValueId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(productOptionValue => new
            {
                productOptionValue.ProductOptionGroupId,
                productOptionValue.OptionValueId
            })
            .IsUnique();
        builder.HasIndex(productOptionValue => new
        {
            productOptionValue.ProductOptionGroupId,
            productOptionValue.IsAvailable,
            productOptionValue.DisplayOrder
        });
        builder.HasQueryFilter(productOptionValue =>
            !productOptionValue.ProductOptionGroup.Product.IsDeleted &&
            !productOptionValue.ProductOptionGroup.OptionGroup.IsDeleted &&
            !productOptionValue.OptionValue.IsDeleted);
    }
}
