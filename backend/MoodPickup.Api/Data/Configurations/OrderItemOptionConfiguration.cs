using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OrderItemOptionConfiguration
    : IEntityTypeConfiguration<OrderItemOption>
{
    public void Configure(EntityTypeBuilder<OrderItemOption> builder)
    {
        builder.ToTable(
            "OrderItemOptions",
            table =>
            {
                table.HasCheckConstraint("CK_OrderItemOptions_PriceModifier_NonNegative", "\"PriceModifier\" >= 0");
                table.HasCheckConstraint("CK_OrderItemOptions_CaloriesModifier_NonNegative", "\"CaloriesModifier\" IS NULL OR \"CaloriesModifier\" >= 0");
                table.HasCheckConstraint("CK_OrderItemOptions_VolumeModifier_NonNegative", "\"VolumeModifier\" IS NULL OR \"VolumeModifier\" >= 0");
                table.HasCheckConstraint("CK_OrderItemOptions_DisplayOrder_NonNegative", "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(option => option.Id);
        builder.Property(option => option.OptionGroupName).HasMaxLength(120).IsRequired();
        builder.Property(option => option.OptionValueName).HasMaxLength(120).IsRequired();
        builder.Property(option => option.PriceModifier).HasPrecision(12, 2);
        builder.HasIndex(option => new { option.OrderItemId, option.DisplayOrder });
    }
}
