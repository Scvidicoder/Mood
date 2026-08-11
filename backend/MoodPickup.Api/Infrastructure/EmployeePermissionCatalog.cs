namespace MoodPickup.Api.Infrastructure;

public sealed record EmployeePermissionDefinition(
    string Permission,
    string DisplayName,
    string Group,
    IReadOnlyList<string> AllowedRoles);

public static class EmployeePermissionCatalog
{
    public const string ViewOrders = "ViewOrders";
    public const string ConfirmOrders = "ConfirmOrders";
    public const string RejectOrders = "RejectOrders";
    public const string CompleteOrders = "CompleteOrders";
    public const string ViewKitchen = "ViewKitchen";
    public const string StartPreparing = "StartPreparing";
    public const string MarkReady = "MarkReady";
    public const string ManageCategories = "ManageCategories";
    public const string ManageProducts = "ManageProducts";
    public const string ManageOptions = "ManageOptions";
    public const string ManageEmployees = "ManageEmployees";
    public const string ViewReports = "ViewReports";
    public const string ManageSettings = "ManageSettings";

    public static IReadOnlyList<EmployeePermissionDefinition> All { get; } =
    [
        Define(ViewOrders, "View Orders", "Orders",
            AuthenticationConstants.Roles.Cashier,
            AuthenticationConstants.Roles.Manager),
        Define(ConfirmOrders, "Confirm Orders", "Orders",
            AuthenticationConstants.Roles.Cashier,
            AuthenticationConstants.Roles.Manager),
        Define(RejectOrders, "Reject Orders", "Orders",
            AuthenticationConstants.Roles.Cashier,
            AuthenticationConstants.Roles.Manager),
        Define(CompleteOrders, "Complete Orders", "Orders",
            AuthenticationConstants.Roles.Pickup,
            AuthenticationConstants.Roles.Cashier),
        Define(ViewKitchen, "View Kitchen", "Kitchen",
            AuthenticationConstants.Roles.Kitchen,
            AuthenticationConstants.Roles.Cashier,
            AuthenticationConstants.Roles.Manager,
            AuthenticationConstants.Roles.Pickup),
        Define(StartPreparing, "Start Preparing", "Kitchen",
            AuthenticationConstants.Roles.Kitchen),
        Define(MarkReady, "Mark Ready", "Kitchen",
            AuthenticationConstants.Roles.Kitchen),
        Define(ManageCategories, "Manage Categories", "Menu",
            AuthenticationConstants.Roles.MenuManager),
        Define(ManageProducts, "Manage Products", "Menu",
            AuthenticationConstants.Roles.MenuManager),
        Define(ManageOptions, "Manage Options", "Menu",
            AuthenticationConstants.Roles.MenuManager),
        Define(ManageEmployees, "Manage Employees", "Employees"),
        Define(ViewReports, "View Reports", "Reports"),
        Define(ManageSettings, "Manage Settings", "Settings")
    ];

    public static bool IsKnown(string permission)
    {
        return All.Any(definition => definition.Permission == permission);
    }

    public static bool IsAllowedByRoles(
        EmployeePermissionDefinition definition,
        IEnumerable<string> roles)
    {
        var currentRoles = roles.ToHashSet(StringComparer.Ordinal);
        return currentRoles.Contains(AuthenticationConstants.Roles.Administrator) ||
               definition.AllowedRoles.Any(currentRoles.Contains);
    }

    private static EmployeePermissionDefinition Define(
        string permission,
        string displayName,
        string group,
        params string[] allowedRoles)
    {
        return new EmployeePermissionDefinition(
            permission,
            displayName,
            group,
            allowedRoles);
    }
}
