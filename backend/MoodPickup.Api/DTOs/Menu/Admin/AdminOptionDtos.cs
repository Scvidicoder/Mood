using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Entities;

namespace MoodPickup.Api.DTOs.Menu.Admin;

public sealed class AdminOptionGroupQuery : PaginationQuery
{
    public bool IncludeDeleted { get; init; }

    public string? Search { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record AdminOptionGroupDto(
    Guid Id,
    string Name,
    string? Description,
    OptionSelectionType SelectionType,
    bool DefaultIsRequired,
    int DefaultMinimumSelections,
    int? DefaultMaximumSelections,
    int DisplayOrder,
    bool IsActive,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion,
    IReadOnlyList<AdminOptionValueDto> Values);

public sealed record AdminOptionValueDto(
    Guid Id,
    Guid OptionGroupId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion);

public sealed record AdminProductOptionGroupDto(
    Guid Id,
    Guid OptionGroupId,
    string OptionGroupName,
    OptionSelectionType SelectionType,
    bool IsRequired,
    int MinimumSelections,
    int MaximumSelections,
    int DisplayOrder,
    bool IsActive,
    bool OptionGroupIsActive,
    bool OptionGroupIsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion,
    IReadOnlyList<AdminProductOptionValueDto> Values);

public sealed record AdminProductOptionValueDto(
    Guid Id,
    Guid OptionValueId,
    string OptionValueName,
    decimal PriceModifier,
    bool IsDefault,
    bool IsAvailable,
    int DisplayOrder,
    int? VolumeMilliliters,
    int? Calories,
    bool OptionValueIsActive,
    bool OptionValueIsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion);

public sealed record CreateOptionGroupRequest(
    string Name,
    string? Description,
    OptionSelectionType SelectionType,
    bool DefaultIsRequired,
    int DefaultMinimumSelections,
    int? DefaultMaximumSelections,
    int DisplayOrder,
    bool IsActive);

public sealed record UpdateOptionGroupRequest(
    string Name,
    string? Description,
    OptionSelectionType SelectionType,
    bool DefaultIsRequired,
    int DefaultMinimumSelections,
    int? DefaultMaximumSelections,
    int DisplayOrder,
    bool IsActive,
    Guid RowVersion);

public sealed record SetActiveRequest(bool IsActive, Guid RowVersion);

public sealed record CreateOptionValueRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public sealed record UpdateOptionValueRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    Guid RowVersion);

public sealed record CreateProductOptionGroupRequest(
    Guid OptionGroupId,
    bool IsRequired,
    int MinimumSelections,
    int MaximumSelections,
    int DisplayOrder,
    bool IsActive);

public sealed record UpdateProductOptionGroupRequest(
    bool IsRequired,
    int MinimumSelections,
    int MaximumSelections,
    int DisplayOrder,
    bool IsActive,
    Guid RowVersion);

public sealed record CreateProductOptionValueRequest(
    Guid OptionValueId,
    decimal PriceModifier,
    bool IsDefault,
    bool IsAvailable,
    int DisplayOrder,
    int? VolumeMilliliters,
    int? Calories);

public sealed record UpdateProductOptionValueRequest(
    decimal PriceModifier,
    bool IsDefault,
    bool IsAvailable,
    int DisplayOrder,
    int? VolumeMilliliters,
    int? Calories,
    Guid RowVersion);
