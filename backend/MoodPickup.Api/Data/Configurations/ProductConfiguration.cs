using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Products_Name_Trimmed",
                    "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                table.HasCheckConstraint(
                    "CK_Products_BasePrice_NonNegative",
                    "\"BasePrice\" >= 0");
                table.HasCheckConstraint(
                    "CK_Products_DefaultWeightGrams_NonNegative",
                    "\"DefaultWeightGrams\" IS NULL OR \"DefaultWeightGrams\" >= 0");
                table.HasCheckConstraint(
                    "CK_Products_DefaultVolumeMilliliters_NonNegative",
                    "\"DefaultVolumeMilliliters\" IS NULL OR \"DefaultVolumeMilliliters\" >= 0");
                table.HasCheckConstraint(
                    "CK_Products_DefaultCalories_NonNegative",
                    "\"DefaultCalories\" IS NULL OR \"DefaultCalories\" >= 0");
                table.HasCheckConstraint(
                    "CK_Products_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(160).IsRequired();
        builder.Property(product => product.NormalizedName).HasMaxLength(160).IsRequired();
        builder.Property(product => product.ShortDescription).HasMaxLength(300);
        builder.Property(product => product.Description).HasMaxLength(2000);
        builder.Property(product => product.Ingredients).HasMaxLength(1000);
        builder.Property(product => product.BasePrice).HasPrecision(12, 2);
        builder.Property(product => product.RowVersion).IsConcurrencyToken();
        builder
            .HasOne(product => product.Category)
            .WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(product => product.Image)
            .WithMany(mediaFile => mediaFile.Products)
            .HasForeignKey(product => product.ImageId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(product => product.NormalizedName);
        builder.HasIndex(product => new
        {
            product.CategoryId,
            product.IsDeleted,
            product.IsVisible,
            product.DisplayOrder
        });
        builder.HasIndex(product => new
        {
            product.IsDeleted,
            product.IsAvailable
        });
        builder.HasQueryFilter(product => !product.IsDeleted);
    }
}
