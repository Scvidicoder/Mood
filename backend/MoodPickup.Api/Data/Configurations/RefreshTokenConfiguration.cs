using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(
            "RefreshTokens",
            table => table.HasCheckConstraint(
                "CK_RefreshTokens_AccountOwner",
                "(\"AccountType\" = 'Customer' AND \"CustomerId\" IS NOT NULL AND \"EmployeeId\" IS NULL) OR " +
                "(\"AccountType\" = 'Employee' AND \"EmployeeId\" IS NOT NULL AND \"CustomerId\" IS NULL)"));
        builder.HasKey(token => token.Id);
        builder.Property(token => token.AccountType).HasConversion<string>().HasMaxLength(16);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.CreatedByIpHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.RevokedByIpHash).HasMaxLength(64);
        builder.Property(token => token.UserAgentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.CustomerId);
        builder.HasIndex(token => token.EmployeeId);
        builder
            .HasOne(token => token.Customer)
            .WithMany(customer => customer.RefreshTokens)
            .HasForeignKey(token => token.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(token => token.Employee)
            .WithMany(employee => employee.RefreshTokens)
            .HasForeignKey(token => token.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(token => token.ReplacedByToken)
            .WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
