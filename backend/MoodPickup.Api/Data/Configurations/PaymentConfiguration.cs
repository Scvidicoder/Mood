using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(
            "Payments",
            table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_Payments_Currency_Uppercase",
                    "\"Currency\" = upper(\"Currency\")");
            });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(payment => payment.ProviderOrderId)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(payment => payment.ProviderTransactionId).HasMaxLength(128);
        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(12, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.FailureReason).HasMaxLength(500);
        builder.Property(payment => payment.RowVersion).IsConcurrencyToken();
        builder.HasIndex(payment => payment.OrderId).IsUnique();
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderOrderId }).IsUnique();
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderTransactionId })
            .IsUnique()
            .HasFilter("\"ProviderTransactionId\" IS NOT NULL");
        builder.HasIndex(payment => new { payment.Status, payment.UpdatedAt });
        builder
            .HasOne(payment => payment.Order)
            .WithOne(order => order.Payment)
            .HasForeignKey<Payment>(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(payment => payment.Attempts)
            .WithOne(attempt => attempt.Payment)
            .HasForeignKey(attempt => attempt.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
