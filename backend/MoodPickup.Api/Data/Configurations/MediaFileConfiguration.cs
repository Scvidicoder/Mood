using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable(
            "MediaFiles",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_MediaFiles_FileSizeBytes_NonNegative",
                    "\"FileSizeBytes\" >= 0");
                table.HasCheckConstraint(
                    "CK_MediaFiles_Width_Positive",
                    "\"Width\" IS NULL OR \"Width\" > 0");
                table.HasCheckConstraint(
                    "CK_MediaFiles_Height_Positive",
                    "\"Height\" IS NULL OR \"Height\" > 0");
            });
        builder.HasKey(mediaFile => mediaFile.Id);
        builder.Property(mediaFile => mediaFile.StorageProvider).HasMaxLength(32).IsRequired();
        builder.Property(mediaFile => mediaFile.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(mediaFile => mediaFile.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(mediaFile => mediaFile.ContentType).HasMaxLength(100).IsRequired();
        builder
            .HasOne(mediaFile => mediaFile.CreatedByEmployee)
            .WithMany()
            .HasForeignKey(mediaFile => mediaFile.CreatedByEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasIndex(mediaFile => new { mediaFile.StorageProvider, mediaFile.StorageKey })
            .IsUnique();
        builder.HasIndex(mediaFile => new { mediaFile.IsDeleted, mediaFile.CreatedAt });
        builder.HasQueryFilter(mediaFile => !mediaFile.IsDeleted);
    }
}
