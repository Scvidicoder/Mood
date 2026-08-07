using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable(
            "Categories",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Categories_Name_Trimmed",
                    "trim(\"Name\") = \"Name\" AND length(\"Name\") > 0");
                table.HasCheckConstraint(
                    "CK_Categories_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(500);
        builder.Property(category => category.RowVersion).IsConcurrencyToken();
        builder.HasIndex(category => category.NormalizedName);
        builder.HasIndex(category => new
        {
            category.IsDeleted,
            category.IsVisible,
            category.DisplayOrder
        });
        builder.HasQueryFilter(category => !category.IsDeleted);
    }
}
