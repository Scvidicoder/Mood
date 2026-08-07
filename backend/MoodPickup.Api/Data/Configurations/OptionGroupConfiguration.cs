using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class OptionGroupConfiguration : IEntityTypeConfiguration<OptionGroup>
{
    public void Configure(EntityTypeBuilder<OptionGroup> builder)
    {
        builder.ToTable(
            "OptionGroups",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OptionGroups_Name_Trimmed",
                    "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                table.HasCheckConstraint(
                    "CK_OptionGroups_DefaultMinimumSelections_NonNegative",
                    "\"DefaultMinimumSelections\" >= 0");
                table.HasCheckConstraint(
                    "CK_OptionGroups_DefaultMaximumSelections_Positive",
                    "\"DefaultMaximumSelections\" IS NULL OR \"DefaultMaximumSelections\" >= 1");
                table.HasCheckConstraint(
                    "CK_OptionGroups_DefaultSelectionRange",
                    "\"DefaultMaximumSelections\" IS NULL OR " +
                    "\"DefaultMinimumSelections\" <= \"DefaultMaximumSelections\"");
                table.HasCheckConstraint(
                    "CK_OptionGroups_SingleMaximum",
                    "\"SelectionType\" <> 'Single' OR " +
                    "\"DefaultMaximumSelections\" IS NULL OR \"DefaultMaximumSelections\" <= 1");
                table.HasCheckConstraint(
                    "CK_OptionGroups_RequiredMinimum",
                    "NOT \"DefaultIsRequired\" OR \"DefaultMinimumSelections\" >= 1");
                table.HasCheckConstraint(
                    "CK_OptionGroups_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(optionGroup => optionGroup.Id);
        builder.Property(optionGroup => optionGroup.Name).HasMaxLength(120).IsRequired();
        builder.Property(optionGroup => optionGroup.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(optionGroup => optionGroup.Description).HasMaxLength(500);
        builder
            .Property(optionGroup => optionGroup.SelectionType)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(optionGroup => optionGroup.RowVersion).IsConcurrencyToken();
        builder.HasIndex(optionGroup => optionGroup.NormalizedName);
        builder.HasIndex(optionGroup => new
        {
            optionGroup.IsDeleted,
            optionGroup.IsActive,
            optionGroup.DisplayOrder
        });
        builder.HasQueryFilter(optionGroup => !optionGroup.IsDeleted);
    }
}
