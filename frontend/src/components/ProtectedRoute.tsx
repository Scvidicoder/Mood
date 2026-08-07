import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import type { AccountType } from "../types/auth";
import { LoadingState } from "./LoadingState";

interface ProtectedRouteProps {
  accountType: AccountType;
  children: React.ReactNode;
}

export function ProtectedRoute({
  accountType,
  children,
}: ProtectedRouteProps) {
  const { session, isInitializing } = useAuth();
  const location = useLocation();

  if (isInitializing) {
    return (
      <section className="page">
        <LoadingState message="Restoring your secure session…" />
      </section>
    );
  }

  if (!session || session.accountType !== accountType) {
    return (
      <Navigate
        replace
        state={{ returnTo: location.pathname }}
        to={accountType === "employee" ? "/staff/login" : "/login"}
      />
    );
  }

  return children;
}
