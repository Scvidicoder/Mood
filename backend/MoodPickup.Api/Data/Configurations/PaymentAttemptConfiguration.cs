using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable(
            "PaymentAttempts",
            table => table.HasCheckConstraint(
                "CK_PaymentAttempts_AttemptNumber_Positive",
                "\"AttemptNumber\" > 0"));
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.ProviderReference)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(attempt => attempt.ProviderStatus)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(attempt => attempt.RequestSnapshot)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(attempt => attempt.ResponseSnapshot).HasColumnType("jsonb");
        builder.HasIndex(attempt => new { attempt.PaymentId, attempt.AttemptNumber }).IsUnique();
        builder.HasIndex(attempt => attempt.ProviderReference).IsUnique();
    }
}
