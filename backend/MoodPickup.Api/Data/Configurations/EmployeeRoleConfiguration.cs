using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class EmployeeRoleConfiguration : IEntityTypeConfiguration<EmployeeRole>
{
    public void Configure(EntityTypeBuilder<EmployeeRole> builder)
    {
        builder.ToTable("EmployeeRoles");
        builder.HasKey(employeeRole => new { employeeRole.EmployeeId, employeeRole.RoleId });
        builder
            .HasOne(employeeRole => employeeRole.Employee)
            .WithMany(employee => employee.EmployeeRoles)
            .HasForeignKey(employeeRole => employeeRole.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(employeeRole => employeeRole.Role)
            .WithMany(role => role.EmployeeRoles)
            .HasForeignKey(employeeRole => employeeRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
