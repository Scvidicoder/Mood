using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

internal static class MenuServiceSupport
{
    public static void EnsureVersion(Guid expected, Guid current, Guid id)
    {
        if (expected == current)
        {
            return;
        }

        throw new ApiProblemException(
            StatusCodes.Status409Conflict,
            "concurrency_conflict",
            "Menu item was changed by another employee",
            "MENU_VERSION_CONFLICT",
            extensions: new Dictionary<string, object?>
            {
                ["currentResource"] = new
                {
                    id,
                    rowVersion = current
                }
            });
    }

    public static void ThrowIfStructurallyInvalid(MenuValidationResult result)
    {
        if (result.IsValid)
        {
            return;
        }

        throw new ApiProblemException(
            StatusCodes.Status409Conflict,
            "business_rule_violation",
            "Menu configuration is structurally invalid",
            "MENU_CONFIGURATION_INVALID",
            extensions: new Dictionary<string, object?>
            {
                ["issues"] = result.Issues.Select(MenuDtoMapper.ToDto).ToArray()
            });
    }

    public static ApiProblemException NotFound(
        string title,
        string code)
    {
        return new ApiProblemException(
            StatusCodes.Status404NotFound,
            "not_found",
            title,
            code);
    }

    public static ApiProblemException Conflict(
        string title,
        string code,
        string? detail = null)
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "business_rule_violation",
            title,
            code,
            detail);
    }

    public static int TotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
