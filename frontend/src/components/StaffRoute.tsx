import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import {
  hasStaffCapability,
  type StaffCapability,
} from "../auth/permissions";
import { ForbiddenPage } from "../pages/ForbiddenPage";
import { LoadingState } from "./LoadingState";

export function StaffRoute({
  capability = "employee",
  children,
}: {
  capability?: StaffCapability;
  children: React.ReactNode;
}) {
  const { session, isInitializing } = useAuth();
  const location = useLocation();

  if (isInitializing) {
    return (
      <section className="page">
        <LoadingState message="Restoring your secure staff session…" />
      </section>
    );
  }

  if (!session) {
    return (
      <Navigate
        replace
        state={{ returnTo: location.pathname }}
        to="/staff/login"
      />
    );
  }

  if (
    session.accountType !== "employee" ||
    !hasStaffCapability(session, capability)
  ) {
    return <ForbiddenPage />;
  }

  return children;
}
