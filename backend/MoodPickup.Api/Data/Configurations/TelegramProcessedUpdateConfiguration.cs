using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class TelegramProcessedUpdateConfiguration
    : IEntityTypeConfiguration<TelegramProcessedUpdate>
{
    public void Configure(EntityTypeBuilder<TelegramProcessedUpdate> builder)
    {
        builder.ToTable("TelegramProcessedUpdates");
        builder.HasKey(update => update.UpdateId);
        builder.HasIndex(update => update.ProcessedAt);
    }
}
