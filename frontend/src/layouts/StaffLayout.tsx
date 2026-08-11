import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { NavLink, Outlet, useNavigate, useOutletContext } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import { hasStaffCapability } from "../auth/permissions";
import { ErrorState } from "../components/ErrorState";
import { useStaffOrderNotifications } from "../hooks/useStaffOrderNotifications";
import type { ConnectionState } from "../hooks/useNotificationsConnection";

interface StaffLayoutContext {
  connectionState: ConnectionState;
}

export function useStaffConnectionState(): ConnectionState {
  return useOutletContext<StaffLayoutContext | undefined>()?.connectionState ??
    "Disconnected";
}

export function StaffLayout() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();
  const connectionState = useStaffOrderNotifications();
  const [navigationOpen, setNavigationOpen] = useState(false);
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => navigate("/staff/login", { replace: true }),
  });
  const canManageMenu = hasStaffCapability(session, "manageMenu");
  const canManageOrders = hasStaffCapability(session, "manageOrders");
  const canViewKitchen = hasStaffCapability(session, "viewKitchen");
  const canViewAudit = hasStaffCapability(session, "viewAuditLog");

  return (
    <div className="staff-shell">
      <aside className={`staff-sidebar ${navigationOpen ? "staff-sidebar--open" : ""}`}>
        <div className="staff-brand">
          <span>Mood Pickup</span>
          <small>Staff operations</small>
        </div>
        <nav aria-label="Staff navigation" className="staff-nav">
          <NavLink end onClick={() => setNavigationOpen(false)} to="/staff">
            Dashboard
          </NavLink>
          {canManageOrders ? (
            <NavLink onClick={() => setNavigationOpen(false)} to="/staff/orders">
              Orders
            </NavLink>
          ) : null}
          {canViewKitchen ? (
            <NavLink onClick={() => setNavigationOpen(false)} to="/staff/kitchen">
              Kitchen
            </NavLink>
          ) : null}
          {canManageMenu ? (
            <>
              <NavLink onClick={() => setNavigationOpen(false)} to="/staff/menu">
                Menu overview
              </NavLink>
              <NavLink
                onClick={() => setNavigationOpen(false)}
                to="/staff/menu/categories"
              >
                Categories
              </NavLink>
              <NavLink
                onClick={() => setNavigationOpen(false)}
                to="/staff/menu/products"
              >
                Products
              </NavLink>
              <NavLink
                onClick={() => setNavigationOpen(false)}
                to="/staff/menu/option-groups"
              >
                Option groups
              </NavLink>
            </>
          ) : null}
          {canViewAudit ? (
            <NavLink onClick={() => setNavigationOpen(false)} to="/staff/audit-log">
              Audit log
            </NavLink>
          ) : null}
          <NavLink onClick={() => setNavigationOpen(false)} to="/staff/profile">
            Profile
          </NavLink>
        </nav>
      </aside>

      <div className="staff-workspace">
        <header className="staff-header">
          <button
            aria-expanded={navigationOpen}
            aria-label="Toggle staff navigation"
            className="icon-button staff-menu-button"
            onClick={() => setNavigationOpen((current) => !current)}
            type="button"
          >
            ☰
          </button>
          <div className="staff-identity">
            <strong>{session?.fullName || session?.username}</strong>
            <span>{session?.roles.join(" · ") || "Employee"}</span>
          </div>
          <div className="staff-session-state">
            <span
              className={`connection-state connection-state--${connectionState.toLowerCase()}`}
            >
              {connectionState}
            </span>
            <span className="status-badge">Authenticated</span>
            <button
              className="button button-secondary button-compact"
              disabled={logoutMutation.isPending}
              onClick={() => logoutMutation.mutate()}
              type="button"
            >
              {logoutMutation.isPending ? "Signing out…" : "Logout"}
            </button>
          </div>
        </header>
        {logoutMutation.error ? (
          <div className="staff-header-error">
            <ErrorState error={logoutMutation.error} />
          </div>
        ) : null}
        <main className="staff-main">
          <Outlet context={{ connectionState } satisfies StaffLayoutContext} />
        </main>
      </div>
    </div>
  );
}
