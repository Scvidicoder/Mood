using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AdminProductConfigurationService(
    MoodPickupDbContext dbContext,
    IMenuConfigurationValidator menuValidator,
    IEmployeeAuditService auditService) : IAdminProductConfigurationService
{
    public async Task<MenuMutationResponse<AdminProductOptionGroupDto>> AddGroupAsync(
        Guid productId,
        CreateProductOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var globalGroup = await dbContext.OptionGroups
            .SingleOrDefaultAsync(
                group => group.Id == request.OptionGroupId,
                cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option group not found",
                "OPTION_GROUP_NOT_FOUND");

        if (product.OptionGroups.Any(group =>
                group.OptionGroupId == request.OptionGroupId))
        {
            throw MenuServiceSupport.Conflict(
                "The option group is already assigned to this product",
                "PRODUCT_OPTION_GROUP_ALREADY_ASSIGNED");
        }

        var assignment = new ProductOptionGroup
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            OptionGroupId = globalGroup.Id,
            OptionGroup = globalGroup,
            IsRequired = request.IsRequired,
            MinimumSelections = request.MinimumSelections,
            MaximumSelections = request.MaximumSelections,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductOptionGroup(assignment));
        product.OptionGroups.Add(assignment);
        dbContext.ProductOptionGroups.Add(assignment);
        await auditService.RecordAsync(
            "ProductOptionGroupAssigned",
            "ProductOptionGroup",
            assignment.Id,
            $"Assigned option group '{globalGroup.Name}' to product '{product.Name}'.",
            null,
            GroupAssignmentAuditValues(assignment),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MenuMutationResponse<AdminProductOptionGroupDto>(
            assignment.ToAdminDto(),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    public async Task<MenuMutationResponse<AdminProductOptionGroupDto>> UpdateGroupAsync(
        Guid productId,
        Guid assignmentId,
        UpdateProductOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var assignment = RequireAssignment(product, assignmentId);
        MenuServiceSupport.EnsureVersion(
            request.RowVersion,
            assignment.RowVersion,
            assignment.Id);
        var oldValues = GroupAssignmentAuditValues(assignment);
        assignment.IsRequired = request.IsRequired;
        assignment.MinimumSelections = request.MinimumSelections;
        assignment.MaximumSelections = request.MaximumSelections;
        assignment.DisplayOrder = request.DisplayOrder;
        assignment.IsActive = request.IsActive;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));

        await auditService.RecordAsync(
            "ProductOptionGroupUpdated",
            "ProductOptionGroup",
            assignment.Id,
            $"Updated option group assignment for product '{product.Name}'.",
            oldValues,
            GroupAssignmentAuditValues(assignment),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MenuMutationResponse<AdminProductOptionGroupDto>(
            assignment.ToAdminDto(),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    public async Task DeleteGroupAsync(
        Guid productId,
        Guid assignmentId,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var assignment = RequireAssignment(product, assignmentId);
        MenuServiceSupport.EnsureVersion(
            rowVersion,
            assignment.RowVersion,
            assignment.Id);
        var oldValue = assignment.IsActive;
        assignment.IsActive = false;
        await auditService.RecordAsync(
            "ProductOptionGroupDisabled",
            "ProductOptionGroup",
            assignment.Id,
            $"Disabled option group assignment for product '{product.Name}'.",
            new { isActive = oldValue },
            new { isActive = false },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MenuMutationResponse<AdminProductOptionGroupDto>> RestoreGroupAsync(
        Guid productId,
        Guid assignmentId,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var assignment = RequireAssignment(product, assignmentId);
        MenuServiceSupport.EnsureVersion(
            request.RowVersion,
            assignment.RowVersion,
            assignment.Id);
        if (assignment.OptionGroup.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "A deleted global option group cannot be restored on a product",
                "MENU_CONFIGURATION_INVALID");
        }

        assignment.IsActive = true;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));
        await auditService.RecordAsync(
            "ProductOptionGroupRestored",
            "ProductOptionGroup",
            assignment.Id,
            $"Restored option group assignment for product '{product.Name}'.",
            new { isActive = false },
            new { isActive = true },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MenuMutationResponse<AdminProductOptionGroupDto>(
            assignment.ToAdminDto(),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    public async Task<MenuMutationResponse<AdminProductOptionValueDto>> AddValueAsync(
        Guid productId,
        Guid assignmentId,
        CreateProductOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var assignment = RequireAssignment(product, assignmentId);
        var optionValue = await dbContext.OptionValues
            .SingleOrDefaultAsync(
                value => value.Id == request.OptionValueId,
                cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option value not found",
                "OPTION_VALUE_NOT_FOUND");

        if (optionValue.OptionGroupId != assignment.OptionGroupId)
        {
            throw MenuServiceSupport.Conflict(
                "The option value belongs to a different global group",
                "OPTION_VALUE_GROUP_MISMATCH");
        }

        if (assignment.Values.Any(value =>
                value.OptionValueId == request.OptionValueId))
        {
            throw MenuServiceSupport.Conflict(
                "The option value is already assigned",
                "PRODUCT_OPTION_VALUE_ALREADY_ASSIGNED");
        }

        EnsureSingleDefaultAllowed(assignment, request.IsDefault, excludingId: null);
        var valueAssignment = new ProductOptionValue
        {
            Id = Guid.NewGuid(),
            ProductOptionGroupId = assignment.Id,
            ProductOptionGroup = assignment,
            OptionValueId = optionValue.Id,
            OptionValue = optionValue,
            PriceModifier = request.PriceModifier,
            IsDefault = request.IsDefault,
            IsAvailable = request.IsAvailable,
            DisplayOrder = request.DisplayOrder,
            VolumeMilliliters = request.VolumeMilliliters,
            Calories = request.Calories
        };
        assignment.Values.Add(valueAssignment);
        dbContext.ProductOptionValues.Add(valueAssignment);
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));
        await auditService.RecordAsync(
            "ProductOptionValueAssigned",
            "ProductOptionValue",
            valueAssignment.Id,
            $"Assigned option value '{optionValue.Name}' to product '{product.Name}'.",
            null,
            ValueAssignmentAuditValues(valueAssignment),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MenuMutationResponse<AdminProductOptionValueDto>(
            valueAssignment.ToAdminDto(),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    public async Task<MenuMutationResponse<AdminProductOptionValueDto>> UpdateValueAsync(
        Guid productId,
        Guid assignmentValueId,
        UpdateProductOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var valueAssignment = product.OptionGroups
            .SelectMany(group => group.Values)
            .SingleOrDefault(value => value.Id == assignmentValueId)
            ?? throw MenuServiceSupport.NotFound(
                "Product option value assignment not found",
                "OPTION_VALUE_NOT_FOUND");
        MenuServiceSupport.EnsureVersion(
            request.RowVersion,
            valueAssignment.RowVersion,
            valueAssignment.Id);
        EnsureSingleDefaultAllowed(
            valueAssignment.ProductOptionGroup,
            request.IsDefault,
            valueAssignment.Id);
        var oldValues = ValueAssignmentAuditValues(valueAssignment);
        valueAssignment.PriceModifier = request.PriceModifier;
        valueAssignment.IsDefault = request.IsDefault;
        valueAssignment.IsAvailable = request.IsAvailable;
        valueAssignment.DisplayOrder = request.DisplayOrder;
        valueAssignment.VolumeMilliliters = request.VolumeMilliliters;
        valueAssignment.Calories = request.Calories;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));
        await auditService.RecordAsync(
            "ProductOptionValueUpdated",
            "ProductOptionValue",
            valueAssignment.Id,
            $"Updated option value assignment for product '{product.Name}'.",
            oldValues,
            ValueAssignmentAuditValues(valueAssignment),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MenuMutationResponse<AdminProductOptionValueDto>(
            valueAssignment.ToAdminDto(),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    public async Task DeleteValueAsync(
        Guid productId,
        Guid assignmentValueId,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(productId, cancellationToken);
        EnsureProductNotDeleted(product);
        var valueAssignment = product.OptionGroups
            .SelectMany(group => group.Values)
            .SingleOrDefault(value => value.Id == assignmentValueId)
            ?? throw MenuServiceSupport.NotFound(
                "Product option value assignment not found",
                "OPTION_VALUE_NOT_FOUND");
        MenuServiceSupport.EnsureVersion(
            rowVersion,
            valueAssignment.RowVersion,
            valueAssignment.Id);
        var oldValues = ValueAssignmentAuditValues(valueAssignment);
        dbContext.ProductOptionValues.Remove(valueAssignment);
        valueAssignment.ProductOptionGroup.Values.Remove(valueAssignment);
        await auditService.RecordAsync(
            "ProductOptionValueRemoved",
            "ProductOptionValue",
            valueAssignment.Id,
            $"Removed option value assignment from product '{product.Name}'.",
            oldValues,
            null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Product> LoadProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
                   .IgnoreQueryFilters()
                   .Include(product => product.Category)
                   .Include(product => product.OptionGroups)
                       .ThenInclude(group => group.OptionGroup)
                   .Include(product => product.OptionGroups)
                       .ThenInclude(group => group.Values)
                           .ThenInclude(value => value.OptionValue)
                   .AsSplitQuery()
                   .SingleOrDefaultAsync(
                       product => product.Id == productId,
                       cancellationToken)
               ?? throw MenuServiceSupport.NotFound(
                   "Product not found",
                   "PRODUCT_NOT_FOUND");
    }

    private static ProductOptionGroup RequireAssignment(
        Product product,
        Guid assignmentId)
    {
        return product.OptionGroups.SingleOrDefault(group => group.Id == assignmentId)
               ?? throw MenuServiceSupport.NotFound(
                   "Product option group assignment not found",
                   "OPTION_GROUP_NOT_FOUND");
    }

    private static void EnsureSingleDefaultAllowed(
        ProductOptionGroup assignment,
        bool requestedDefault,
        Guid? excludingId)
    {
        if (!requestedDefault ||
            assignment.OptionGroup.SelectionType != OptionSelectionType.Single)
        {
            return;
        }

        if (assignment.Values.Any(value =>
                value.IsDefault &&
                (!excludingId.HasValue || value.Id != excludingId.Value)))
        {
            throw MenuServiceSupport.Conflict(
                "A single-selection group can have at most one default",
                "MULTIPLE_DEFAULT_VALUES_NOT_ALLOWED");
        }
    }

    private static void EnsureProductNotDeleted(Product product)
    {
        if (product.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Product is already deleted",
                "PRODUCT_ALREADY_DELETED");
        }
    }

    private static object GroupAssignmentAuditValues(ProductOptionGroup assignment)
    {
        return new
        {
            assignment.ProductId,
            assignment.OptionGroupId,
            assignment.IsRequired,
            assignment.MinimumSelections,
            assignment.MaximumSelections,
            assignment.DisplayOrder,
            assignment.IsActive
        };
    }

    private static object ValueAssignmentAuditValues(ProductOptionValue assignment)
    {
        return new
        {
            assignment.ProductOptionGroupId,
            assignment.OptionValueId,
            assignment.PriceModifier,
            assignment.IsDefault,
            assignment.IsAvailable,
            assignment.DisplayOrder,
            assignment.VolumeMilliliters,
            assignment.Calories
        };
    }
}
