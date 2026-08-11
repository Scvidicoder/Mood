import { Link } from "react-router-dom";
import { useAuth } from "../../app/AuthProvider";
import { hasStaffCapability } from "../../auth/permissions";

export function StaffDashboardPage() {
  const { session } = useAuth();
  const canManageMenu = hasStaffCapability(session, "manageMenu");
  const canManageOrders = hasStaffCapability(session, "manageOrders");
  const canViewKitchen = hasStaffCapability(session, "viewKitchen");

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Staff dashboard</p>
          <h1>Welcome, {session?.fullName || session?.username}</h1>
          <p>
            Use the sections available for your assigned roles. Order staff can
            review and respond to orders from this workspace.
          </p>
        </div>
      </div>
      {session?.mustChangePassword ? (
        <div className="notice notice--warning">
          <strong>Password change required.</strong>
          <p>Set a permanent password before operational policies are enabled.</p>
          <Link className="button button-link" to="/staff/profile">
            Change password
          </Link>
        </div>
      ) : null}
      <div className="summary-grid">
        {canManageOrders ? (
          <Link className="summary-card summary-card--link" to="/staff/orders">
            <span>Pending orders</span>
            <strong>Open dashboard</strong>
            <small>Confirm, reject, and manage estimated ready times</small>
          </Link>
        ) : null}
        {canViewKitchen ? (
          <Link className="summary-card summary-card--link" to="/staff/kitchen">
            <span>Kitchen workflow</span>
            <strong>Open dashboard</strong>
            <small>Track confirmed, preparing, and ready orders live</small>
          </Link>
        ) : null}
        {canManageMenu ? (
          <Link className="summary-card summary-card--link" to="/staff/menu">
            <span>Menu administration</span>
            <strong>Open workspace</strong>
            <small>Categories, products, options, and images</small>
          </Link>
        ) : (
          <div className="summary-card">
            <span>Assigned roles</span>
            <strong>{session?.roles.length ?? 0}</strong>
            <small>{session?.roles.join(", ") || "No operational roles"}</small>
          </div>
        )}
      </div>
    </section>
  );
}
