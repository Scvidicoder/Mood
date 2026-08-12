using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class EmployeeActionLogConfiguration
    : IEntityTypeConfiguration<EmployeeActionLog>
{
    public void Configure(EntityTypeBuilder<EmployeeActionLog> builder)
    {
        builder.ToTable("EmployeeActionLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.ActionType).HasMaxLength(80).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(log => log.Description).HasMaxLength(500).IsRequired();
        builder.Property(log => log.OldValuesJson).HasColumnType("jsonb");
        builder.Property(log => log.NewValuesJson).HasColumnType("jsonb");
        builder.Property(log => log.CorrelationId).HasMaxLength(100).IsRequired();
        builder
            .HasOne(log => log.Employee)
            .WithMany(employee => employee.ActionLogs)
            .HasForeignKey(log => log.EmployeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(log => new { log.CreatedAt, log.Id });
        builder.HasIndex(log => new { log.EmployeeId, log.CreatedAt });
        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt });
        builder.HasIndex(log => new { log.ActionType, log.CreatedAt });
    }
}
