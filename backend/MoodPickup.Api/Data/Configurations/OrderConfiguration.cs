using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(
            "Orders",
            table =>
            {
                table.HasCheckConstraint("CK_Orders_Subtotal_NonNegative", "\"Subtotal\" >= 0");
                table.HasCheckConstraint("CK_Orders_DiscountTotal_NonNegative", "\"DiscountTotal\" >= 0");
                table.HasCheckConstraint("CK_Orders_Total_NonNegative", "\"Total\" >= 0");
                table.HasCheckConstraint(
                    "CK_Orders_Total_Matches_Subtotal_Discount",
                    "\"Total\" = \"Subtotal\" - \"DiscountTotal\"");
                table.HasCheckConstraint(
                    "CK_Orders_ScheduledPickupRequiresTime",
                    "(\"PickupMode\" = 'AsSoonAsPossible' AND \"RequestedPickupTime\" IS NULL) OR (\"PickupMode\" = 'Scheduled' AND \"RequestedPickupTime\" IS NOT NULL)");
            });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.OrderNumber).HasMaxLength(32).IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(order => order.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(order => order.PickupMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(order => order.CustomerName).HasMaxLength(100).IsRequired();
        builder.Property(order => order.CustomerPhoneNumber).HasMaxLength(16).IsRequired();
        builder.Property(order => order.Comment).HasMaxLength(500);
        builder.Property(order => order.Subtotal).HasPrecision(12, 2);
        builder.Property(order => order.DiscountTotal).HasPrecision(12, 2);
        builder.Property(order => order.Total).HasPrecision(12, 2);
        builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();
        builder.Property(order => order.RejectReason).HasMaxLength(500);
        builder.Property(order => order.RowVersion).IsConcurrencyToken();
        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt });
        builder.HasIndex(order => new { order.Status, order.CreatedAt });
        builder
            .HasOne(order => order.Customer)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(order => order.ConfirmedByEmployee)
            .WithMany()
            .HasForeignKey(order => order.ConfirmedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(order => order.RejectedByEmployee)
            .WithMany()
            .HasForeignKey(order => order.RejectedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
