import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";

export function ProfilePage() {
  const navigate = useNavigate();
  const { session, logout } = useAuth();
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => navigate("/", { replace: true }),
  });

  return (
    <section className="page">
      <div className="page-heading">
        <p className="eyebrow">Authenticated customer</p>
        <h1>Your profile</h1>
        <p>
          Authentication is active. Profile editing and ordering features belong
          to later sprints.
        </p>
      </div>
      <div className="placeholder-card account-card">
        <dl className="status-details">
          <dt>Account ID</dt>
          <dd>{session?.accountId}</dd>
          <dt>Phone</dt>
          <dd>{session?.phoneNumber}</dd>
          <dt>Token storage</dt>
          <dd>Access token in memory; refresh token in HttpOnly cookie</dd>
        </dl>
        {logoutMutation.error ? <ErrorState error={logoutMutation.error} /> : null}
        <button
          className="button button-secondary"
          disabled={logoutMutation.isPending}
          onClick={() => logoutMutation.mutate()}
          type="button"
        >
          {logoutMutation.isPending ? "Signing out…" : "Sign out"}
        </button>
      </div>
    </section>
  );
}
