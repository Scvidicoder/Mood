using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class EmployeePermissionConfiguration
    : IEntityTypeConfiguration<EmployeePermission>
{
    public void Configure(EntityTypeBuilder<EmployeePermission> builder)
    {
        builder.ToTable("EmployeePermissions");
        builder.HasKey(permission => new
        {
            permission.EmployeeId,
            permission.Permission
        });
        builder.Property(permission => permission.Permission)
            .HasMaxLength(80)
            .IsRequired();
        builder
            .HasOne(permission => permission.Employee)
            .WithMany(employee => employee.PermissionOverrides)
            .HasForeignKey(permission => permission.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
