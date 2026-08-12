using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class PaymentWebhookEventConfiguration
    : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");
        builder.HasKey(webhook => webhook.Id);
        builder.Property(webhook => webhook.Provider)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(webhook => webhook.EventIdentifier)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(webhook => webhook.PayloadHash)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(webhook => webhook.ProcessingResult)
            .HasMaxLength(160)
            .IsRequired();
        builder.HasIndex(webhook => new { webhook.Provider, webhook.EventIdentifier }).IsUnique();
        builder.HasIndex(webhook => new { webhook.ReceivedAt, webhook.Id });
    }
}
