import type { AuthSession } from "../types/auth";

export type StaffCapability =
  | "employee"
  | "manageMenu"
  | "manageOrders"
  | "viewKitchen"
  | "workKitchen"
  | "completeOrders"
  | "manageEmployees"
  | "viewAuditLog";

export function hasStaffCapability(
  session: AuthSession | null,
  capability: StaffCapability,
): boolean {
  if (
    !session ||
    session.accountType !== "employee" ||
    session.mustChangePassword
  ) {
    return capability === "employee" &&
      Boolean(session?.accountType === "employee");
  }

  if (capability === "employee") {
    return true;
  }

  const isAdministrator = session.roles.includes("Administrator");
  if (capability === "manageMenu") {
    return isAdministrator || session.roles.includes("MenuManager");
  }

  if (capability === "manageOrders") {
    return (
      isAdministrator ||
      session.roles.includes("Cashier") ||
      session.roles.includes("Manager")
    );
  }

  if (capability === "viewKitchen") {
    return (
      isAdministrator ||
      session.roles.some((role) =>
        ["Kitchen", "Cashier", "Manager", "Pickup"].includes(role),
      )
    );
  }

  if (capability === "workKitchen") {
    return isAdministrator || session.roles.includes("Kitchen");
  }

  if (capability === "completeOrders") {
    return (
      isAdministrator ||
      session.roles.includes("Cashier") ||
      session.roles.includes("Pickup")
    );
  }

  if (capability === "manageEmployees") {
    return isAdministrator;
  }

  return isAdministrator;
}
