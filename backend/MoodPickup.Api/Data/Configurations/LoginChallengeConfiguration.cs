using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class LoginChallengeConfiguration : IEntityTypeConfiguration<LoginChallenge>
{
    public void Configure(EntityTypeBuilder<LoginChallenge> builder)
    {
        builder.ToTable("LoginChallenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.PhoneNumber).HasMaxLength(16).IsRequired();
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(64);
        builder.Property(challenge => challenge.TelegramUsername).HasMaxLength(64);
        builder.Property(challenge => challenge.TelegramLinkTokenHash).HasMaxLength(64);
        builder.Property(challenge => challenge.ClientStatusSecretHash).HasMaxLength(64);
        builder.Property(challenge => challenge.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.Property(challenge => challenge.RequestIpHash).HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.UserAgentHash).HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.RowVersion).IsConcurrencyToken();
        builder.HasIndex(challenge => new { challenge.PhoneNumber, challenge.CreatedAt });
        builder.HasIndex(challenge => new { challenge.RequestIpHash, challenge.CreatedAt });
        builder.HasIndex(challenge => challenge.TelegramLinkTokenHash)
            .IsUnique()
            .HasFilter("\"TelegramLinkTokenHash\" IS NOT NULL");
        builder.HasIndex(challenge => challenge.ClientStatusSecretHash)
            .IsUnique()
            .HasFilter("\"ClientStatusSecretHash\" IS NOT NULL");
        builder.HasIndex(challenge => new
        {
            challenge.TelegramUserId,
            challenge.TelegramStartedAt
        });
    }
}
