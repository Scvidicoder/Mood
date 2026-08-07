using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Data.Configurations;

public sealed class ProductOptionGroupConfiguration
    : IEntityTypeConfiguration<ProductOptionGroup>
{
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.ToTable(
            "ProductOptionGroups",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ProductOptionGroups_MinimumSelections_NonNegative",
                    "\"MinimumSelections\" >= 0");
                table.HasCheckConstraint(
                    "CK_ProductOptionGroups_MaximumSelections_Positive",
                    "\"MaximumSelections\" >= 1");
                table.HasCheckConstraint(
                    "CK_ProductOptionGroups_SelectionRange",
                    "\"MinimumSelections\" <= \"MaximumSelections\"");
                table.HasCheckConstraint(
                    "CK_ProductOptionGroups_RequiredMinimum",
                    "NOT \"IsRequired\" OR \"MinimumSelections\" >= 1");
                table.HasCheckConstraint(
                    "CK_ProductOptionGroups_DisplayOrder_NonNegative",
                    "\"DisplayOrder\" >= 0");
            });
        builder.HasKey(productOptionGroup => productOptionGroup.Id);
        builder.Property(productOptionGroup => productOptionGroup.RowVersion).IsConcurrencyToken();
        builder
            .HasOne(productOptionGroup => productOptionGroup.Product)
            .WithMany(product => product.OptionGroups)
            .HasForeignKey(productOptionGroup => productOptionGroup.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne(productOptionGroup => productOptionGroup.OptionGroup)
            .WithMany(optionGroup => optionGroup.ProductAssignments)
            .HasForeignKey(productOptionGroup => productOptionGroup.OptionGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasIndex(productOptionGroup => new
            {
                productOptionGroup.ProductId,
                productOptionGroup.OptionGroupId
            })
            .IsUnique();
        builder.HasIndex(productOptionGroup => new
        {
            productOptionGroup.ProductId,
            productOptionGroup.IsActive,
            productOptionGroup.DisplayOrder
        });
        builder.HasQueryFilter(productOptionGroup =>
            !productOptionGroup.Product.IsDeleted &&
            !productOptionGroup.OptionGroup.IsDeleted);
    }
}
