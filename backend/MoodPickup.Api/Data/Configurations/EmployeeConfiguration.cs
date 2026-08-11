using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(employee => employee.Id);
        builder.Property(employee => employee.Username).HasMaxLength(64).IsRequired();
        builder.Property(employee => employee.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(employee => employee.FullName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.RowVersion).IsConcurrencyToken();
        builder.HasIndex(employee => employee.Username).IsUnique();
        builder.HasIndex(employee => employee.IsDeleted);
    }
}
