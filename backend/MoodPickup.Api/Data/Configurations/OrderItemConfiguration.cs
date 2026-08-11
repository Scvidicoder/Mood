using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable(
            "OrderItems",
            table =>
            {
                table.HasCheckConstraint("CK_OrderItems_BasePrice_NonNegative", "\"BasePrice\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_FinalPrice_NonNegative", "\"FinalPrice\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_Quantity_Range", "\"Quantity\" >= 1 AND \"Quantity\" <= 99");
                table.HasCheckConstraint("CK_OrderItems_Calories_NonNegative", "\"Calories\" IS NULL OR \"Calories\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_Volume_NonNegative", "\"VolumeMilliliters\" IS NULL OR \"VolumeMilliliters\" >= 0");
                table.HasCheckConstraint("CK_OrderItems_Weight_NonNegative", "\"WeightGrams\" IS NULL OR \"WeightGrams\" >= 0");
            });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ProductName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.IsAvailableAtPurchase).IsRequired();
        builder.Property(item => item.BasePrice).HasPrecision(12, 2);
        builder.Property(item => item.FinalPrice).HasPrecision(12, 2);
        builder.Property(item => item.Comment).HasMaxLength(500);
        builder.HasIndex(item => item.OrderId);
        builder
            .HasMany(item => item.Options)
            .WithOne(option => option.OrderItem)
            .HasForeignKey(option => option.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
