using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AdminOptionService(
    MoodPickupDbContext dbContext,
    IMenuConfigurationValidator menuValidator,
    IEmployeeAuditService auditService) : IAdminOptionService
{
    public async Task<PagedResponse<AdminOptionGroupDto>> GetGroupsAsync(
        AdminOptionGroupQuery query,
        CancellationToken cancellationToken)
    {
        var groups = dbContext.OptionGroups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(group => query.IncludeDeleted || !group.IsDeleted);
        var search = query.Search?.Trim().ToLowerInvariant();

        if (query.IsActive is bool isActive)
        {
            groups = groups.Where(group => group.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            groups = groups.Where(group =>
                group.NormalizedName.Contains(search) ||
                (group.Description != null &&
                 group.Description.ToLower().Contains(search)));
        }

        var totalCount = await groups.CountAsync(cancellationToken);
        var entities = await groups
            .Include(group => group.Values)
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (!query.IncludeDeleted)
        {
            foreach (var group in entities)
            {
                group.Values = group.Values.Where(value => !value.IsDeleted).ToList();
            }
        }

        return new PagedResponse<AdminOptionGroupDto>(
            entities.Select(MenuDtoMapper.ToAdminDto).ToArray(),
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<AdminOptionGroupDto> GetGroupAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var group = await dbContext.OptionGroups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Values)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option group not found",
                "OPTION_GROUP_NOT_FOUND");
        return group.ToAdminDto();
    }

    public async Task<AdminOptionGroupDto> CreateGroupAsync(
        CreateOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = new OptionGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            SelectionType = request.SelectionType,
            DefaultIsRequired = request.DefaultIsRequired,
            DefaultMinimumSelections = request.DefaultMinimumSelections,
            DefaultMaximumSelections = request.DefaultMaximumSelections,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateOptionGroup(group));
        dbContext.OptionGroups.Add(group);

        await auditService.RecordAsync(
            "OptionGroupCreated",
            "OptionGroup",
            group.Id,
            $"Created option group '{group.Name}'.",
            null,
            GroupAuditValues(group),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return group.ToAdminDto();
    }

    public async Task<AdminOptionGroupDto> UpdateGroupAsync(
        Guid id,
        UpdateOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await GetTrackedGroupAsync(id, cancellationToken);
        EnsureGroupNotDeleted(group);
        MenuServiceSupport.EnsureVersion(request.RowVersion, group.RowVersion, id);
        var oldValues = GroupAuditValues(group);

        group.Name = request.Name;
        group.Description = request.Description;
        group.SelectionType = request.SelectionType;
        group.DefaultIsRequired = request.DefaultIsRequired;
        group.DefaultMinimumSelections = request.DefaultMinimumSelections;
        group.DefaultMaximumSelections = request.DefaultMaximumSelections;
        group.DisplayOrder = request.DisplayOrder;
        group.IsActive = request.IsActive;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateOptionGroup(group));

        if (group.SelectionType == OptionSelectionType.Single &&
            await dbContext.ProductOptionGroups
                .IgnoreQueryFilters()
                .AnyAsync(
                    assignment =>
                        assignment.OptionGroupId == id &&
                        assignment.MaximumSelections != 1,
                    cancellationToken))
        {
            throw MenuServiceSupport.Conflict(
                "Existing product assignments are incompatible with a single-selection group",
                "INVALID_OPTION_SELECTION_RULES");
        }

        await auditService.RecordAsync(
            "OptionGroupUpdated",
            "OptionGroup",
            group.Id,
            $"Updated option group '{group.Name}'.",
            oldValues,
            GroupAuditValues(group),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return group.ToAdminDto();
    }

    public async Task<AdminOptionGroupDto> SetGroupActiveAsync(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var group = await GetTrackedGroupAsync(id, cancellationToken);
        EnsureGroupNotDeleted(group);
        MenuServiceSupport.EnsureVersion(request.RowVersion, group.RowVersion, id);
        var oldValue = group.IsActive;
        group.IsActive = request.IsActive;
        await auditService.RecordAsync(
            "OptionGroupActiveChanged",
            "OptionGroup",
            group.Id,
            $"Changed active state for option group '{group.Name}'.",
            new { isActive = oldValue },
            new { isActive = group.IsActive },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return group.ToAdminDto();
    }

    public async Task DeleteGroupAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var group = await GetTrackedGroupAsync(id, cancellationToken);
        if (group.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Option group is already deleted",
                "OPTION_GROUP_ALREADY_DELETED");
        }

        MenuServiceSupport.EnsureVersion(rowVersion, group.RowVersion, id);
        group.IsDeleted = true;
        await auditService.RecordAsync(
            "OptionGroupDeleted",
            "OptionGroup",
            group.Id,
            $"Soft-deleted option group '{group.Name}'.",
            new { isDeleted = false },
            new { isDeleted = true },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminOptionGroupDto> RestoreGroupAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        var group = await GetTrackedGroupAsync(id, cancellationToken);
        MenuServiceSupport.EnsureVersion(request.RowVersion, group.RowVersion, id);
        group.IsDeleted = false;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateOptionGroup(group));
        await auditService.RecordAsync(
            "OptionGroupRestored",
            "OptionGroup",
            group.Id,
            $"Restored option group '{group.Name}'.",
            new { isDeleted = true },
            new { isDeleted = false },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return group.ToAdminDto();
    }

    public async Task<IReadOnlyList<AdminOptionValueDto>> GetValuesAsync(
        Guid groupId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.OptionGroups
                .IgnoreQueryFilters()
                .AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            throw MenuServiceSupport.NotFound(
                "Option group not found",
                "OPTION_GROUP_NOT_FOUND");
        }

        return await dbContext.OptionValues
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(value =>
                value.OptionGroupId == groupId &&
                (includeDeleted || !value.IsDeleted))
            .OrderBy(value => value.DisplayOrder)
            .ThenBy(value => value.Name)
            .Select(value => new AdminOptionValueDto(
                value.Id,
                value.OptionGroupId,
                value.Name,
                value.Description,
                value.DisplayOrder,
                value.IsActive,
                value.IsDeleted,
                value.CreatedAt,
                value.UpdatedAt,
                value.RowVersion))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminOptionValueDto> CreateValueAsync(
        Guid groupId,
        CreateOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        var group = await dbContext.OptionGroups
            .SingleOrDefaultAsync(item => item.Id == groupId, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option group not found",
                "OPTION_GROUP_NOT_FOUND");
        await EnsureValueNameAvailableAsync(
            groupId,
            request.Name,
            excludingId: null,
            cancellationToken);

        var value = new OptionValue
        {
            Id = Guid.NewGuid(),
            OptionGroupId = group.Id,
            OptionGroup = group,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive
        };
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateOptionValue(value));
        dbContext.OptionValues.Add(value);
        await auditService.RecordAsync(
            "OptionValueCreated",
            "OptionValue",
            value.Id,
            $"Created option value '{value.Name}'.",
            null,
            ValueAuditValues(value),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return value.ToAdminDto();
    }

    public async Task<AdminOptionValueDto> UpdateValueAsync(
        Guid id,
        UpdateOptionValueRequest request,
        CancellationToken cancellationToken)
    {
        var value = await GetTrackedValueAsync(id, cancellationToken);
        EnsureValueNotDeleted(value);
        MenuServiceSupport.EnsureVersion(request.RowVersion, value.RowVersion, id);
        await EnsureValueNameAvailableAsync(
            value.OptionGroupId,
            request.Name,
            id,
            cancellationToken);
        var oldValues = ValueAuditValues(value);
        value.Name = request.Name;
        value.Description = request.Description;
        value.DisplayOrder = request.DisplayOrder;
        value.IsActive = request.IsActive;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateOptionValue(value));
        await auditService.RecordAsync(
            "OptionValueUpdated",
            "OptionValue",
            value.Id,
            $"Updated option value '{value.Name}'.",
            oldValues,
            ValueAuditValues(value),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return value.ToAdminDto();
    }

    public async Task<AdminOptionValueDto> SetValueActiveAsync(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        var value = await GetTrackedValueAsync(id, cancellationToken);
        EnsureValueNotDeleted(value);
        MenuServiceSupport.EnsureVersion(request.RowVersion, value.RowVersion, id);
        var oldValue = value.IsActive;
        value.IsActive = request.IsActive;
        await auditService.RecordAsync(
            "OptionValueActiveChanged",
            "OptionValue",
            value.Id,
            $"Changed active state for option value '{value.Name}'.",
            new { isActive = oldValue },
            new { isActive = value.IsActive },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return value.ToAdminDto();
    }

    public async Task DeleteValueAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var value = await GetTrackedValueAsync(id, cancellationToken);
        if (value.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Option value is already deleted",
                "OPTION_VALUE_ALREADY_DELETED");
        }

        MenuServiceSupport.EnsureVersion(rowVersion, value.RowVersion, id);
        value.IsDeleted = true;
        await auditService.RecordAsync(
            "OptionValueDeleted",
            "OptionValue",
            value.Id,
            $"Soft-deleted option value '{value.Name}'.",
            new { isDeleted = false },
            new { isDeleted = true },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminOptionValueDto> RestoreValueAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        var value = await GetTrackedValueAsync(id, cancellationToken);
        MenuServiceSupport.EnsureVersion(request.RowVersion, value.RowVersion, id);
        var groupExists = await dbContext.OptionGroups
            .AnyAsync(group => group.Id == value.OptionGroupId, cancellationToken);
        if (!groupExists)
        {
            throw MenuServiceSupport.Conflict(
                "An option value cannot be restored into a deleted group",
                "MENU_CONFIGURATION_INVALID");
        }

        await EnsureValueNameAvailableAsync(
            value.OptionGroupId,
            value.Name,
            value.Id,
            cancellationToken);
        value.IsDeleted = false;
        await auditService.RecordAsync(
            "OptionValueRestored",
            "OptionValue",
            value.Id,
            $"Restored option value '{value.Name}'.",
            new { isDeleted = true },
            new { isDeleted = false },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return value.ToAdminDto();
    }

    private async Task<OptionGroup> GetTrackedGroupAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.OptionGroups
            .IgnoreQueryFilters()
            .Include(group => group.Values)
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option group not found",
                "OPTION_GROUP_NOT_FOUND");
    }

    private async Task<OptionValue> GetTrackedValueAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.OptionValues
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Option value not found",
                "OPTION_VALUE_NOT_FOUND");
    }

    private async Task EnsureValueNameAvailableAsync(
        Guid groupId,
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var duplicate = await dbContext.OptionValues
            .IgnoreQueryFilters()
            .AnyAsync(
                value =>
                    value.OptionGroupId == groupId &&
                    !value.IsDeleted &&
                    value.NormalizedName == normalizedName &&
                    (!excludingId.HasValue || value.Id != excludingId.Value),
                cancellationToken);
        if (duplicate)
        {
            throw MenuServiceSupport.Conflict(
                "An active option value with this name already exists in the group",
                "DUPLICATE_OPTION_VALUE_NAME");
        }
    }

    private static void EnsureGroupNotDeleted(OptionGroup group)
    {
        if (group.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Option group is already deleted",
                "OPTION_GROUP_ALREADY_DELETED");
        }
    }

    private static void EnsureValueNotDeleted(OptionValue value)
    {
        if (value.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Option value is already deleted",
                "OPTION_VALUE_ALREADY_DELETED");
        }
    }

    private static object GroupAuditValues(OptionGroup group)
    {
        return new
        {
            group.Name,
            group.SelectionType,
            group.DefaultIsRequired,
            group.DefaultMinimumSelections,
            group.DefaultMaximumSelections,
            group.DisplayOrder,
            group.IsActive,
            group.IsDeleted
        };
    }

    private static object ValueAuditValues(OptionValue value)
    {
        return new
        {
            value.OptionGroupId,
            value.Name,
            value.DisplayOrder,
            value.IsActive,
            value.IsDeleted
        };
    }
}
