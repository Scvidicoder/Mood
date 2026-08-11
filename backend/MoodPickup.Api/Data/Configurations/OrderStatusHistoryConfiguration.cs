using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OrderStatusHistoryConfiguration
    : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.OldStatus)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(history => history.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(history => history.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(history => history.Reason).HasMaxLength(500);
        builder.HasIndex(history => new { history.OrderId, history.Timestamp });
        builder
            .HasOne(history => history.Order)
            .WithMany(order => order.StatusHistory)
            .HasForeignKey(history => history.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(history => history.Employee)
            .WithMany()
            .HasForeignKey(history => history.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
