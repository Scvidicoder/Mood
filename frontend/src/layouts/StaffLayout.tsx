import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import { hasStaffCapability } from "../auth/permissions";
import { ErrorState } from "../components/ErrorState";
import { useNotificationsConnection } from "../hooks/useNotificationsConnection";

export function StaffLayout() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();
  const connectionState = useNotificationsConnection();
  const [navigationOpen, setNavigationOpen] = useState(false);
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => navigate("/staff/login", { replace: true }),
  });
  const canManageMenu = hasStaffCapability(session, "manageMenu");
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
          <Outlet />
        </main>
      </div>
    </div>
  );
}
