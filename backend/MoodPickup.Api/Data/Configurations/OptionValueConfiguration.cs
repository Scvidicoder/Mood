using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OptionValueConfiguration : IEntityTypeConfiguration<OptionValue>
{
    public void Configure(EntityTypeBuilder<OptionValue> builder)
    {
        builder.ToTable(
            "OptionValues",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OptionValues_Name_Trimmed",
                    "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                table.HasCheckConstraint(
                    "CK_OptionValues_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(optionValue => optionValue.Id);
        builder.Property(optionValue => optionValue.Name).HasMaxLength(120).IsRequired();
        builder.Property(optionValue => optionValue.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(optionValue => optionValue.Description).HasMaxLength(500);
        builder.Property(optionValue => optionValue.RowVersion).IsConcurrencyToken();
        builder
            .HasOne(optionValue => optionValue.OptionGroup)
            .WithMany(optionGroup => optionGroup.Values)
            .HasForeignKey(optionValue => optionValue.OptionGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(optionValue => new
            {
                optionValue.OptionGroupId,
                optionValue.NormalizedName
            })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
        builder.HasIndex(optionValue => new
        {
            optionValue.OptionGroupId,
            optionValue.IsDeleted,
            optionValue.IsActive,
            optionValue.DisplayOrder
        });
        builder.HasQueryFilter(optionValue => !optionValue.IsDeleted);
    }
}
