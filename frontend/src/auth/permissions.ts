import type { AuthSession } from "../types/auth";

export type StaffCapability = "employee" | "manageMenu" | "viewAuditLog";

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

  return isAdministrator;
}
