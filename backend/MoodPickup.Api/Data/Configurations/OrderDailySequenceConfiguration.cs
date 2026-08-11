using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OrderDailySequenceConfiguration
    : IEntityTypeConfiguration<OrderDailySequence>
{
    public void Configure(EntityTypeBuilder<OrderDailySequence> builder)
    {
        builder.ToTable(
            "OrderDailySequences",
            table => table.HasCheckConstraint(
                "CK_OrderDailySequences_LastValue_Positive",
                "\"LastValue\" >= 1"));
        builder.HasKey(sequence => sequence.OrderDate);
        builder.Property(sequence => sequence.OrderDate).HasColumnType("date");
    }
}
