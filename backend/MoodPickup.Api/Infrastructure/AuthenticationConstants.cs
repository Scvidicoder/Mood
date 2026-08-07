namespace MoodPickup.Api.Infrastructure;

public static class AuthenticationConstants
{
    public const string AccountTypeClaim = "account_type";
    public const string MustChangePasswordClaim = "must_change_password";
    public const string RegistrationTokenUse = "registration";
    public const string TokenUseClaim = "token_use";
    public const string ChallengeIdClaim = "challenge_id";

    public static class AccountTypes
    {
        public const string Customer = "customer";
        public const string Employee = "employee";
    }

    public static class Roles
    {
        public const string Administrator = "Administrator";
        public const string OrderReception = "OrderReception";
        public const string Kitchen = "Kitchen";
        public const string Pickup = "Pickup";
        public const string MenuManager = "MenuManager";

        public static readonly string[] All =
        [
            Administrator,
            OrderReception,
            Kitchen,
            Pickup,
            MenuManager
        ];
    }

    public static class Policies
    {
        public const string Customer = "Customer";
        public const string Employee = "Employee";
        public const string CanReceiveOrders = "CanReceiveOrders";
        public const string CanWorkKitchen = "CanWorkKitchen";
        public const string CanIssueOrders = "CanIssueOrders";
        public const string CanManageMenu = "CanManageMenu";
        public const string CanManageEmployees = "CanManageEmployees";
        public const string CanManageCafeSettings = "CanManageCafeSettings";
        public const string CanViewAuditLog = "CanViewAuditLog";
    }
}
