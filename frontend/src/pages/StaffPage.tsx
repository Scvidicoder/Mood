import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { changeEmployeePassword } from "../api/auth";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";

export function StaffPage() {
  const navigate = useNavigate();
  const { session, refreshSession, logout } = useAuth();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const changePassword = useMutation({
    mutationFn: () => changeEmployeePassword(currentPassword, newPassword),
    onSuccess: async () => {
      setCurrentPassword("");
      setNewPassword("");
      await refreshSession();
    },
  });
  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => navigate("/", { replace: true }),
  });

  function handlePasswordChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    changePassword.mutate();
  }

  return (
    <section className="page">
      <div className="page-heading">
        <p className="eyebrow">Authenticated employee</p>
        <h1>Staff area</h1>
        <p>
          Your employee session and authorization claims are active. Operational
          dashboards remain outside this sprint.
        </p>
      </div>

      {session?.mustChangePassword ? (
        <div className="auth-card auth-card--inline">
          <h2>Change your temporary password</h2>
          <p>You must set a permanent password before staff policies are enabled.</p>
          <form className="auth-form" onSubmit={handlePasswordChange}>
            <label htmlFor="current-password">Current password</label>
            <input
              autoComplete="current-password"
              id="current-password"
              onChange={(event) => setCurrentPassword(event.target.value)}
              required
              type="password"
              value={currentPassword}
            />
            <label htmlFor="new-password">New password</label>
            <input
              autoComplete="new-password"
              id="new-password"
              minLength={12}
              onChange={(event) => setNewPassword(event.target.value)}
              required
              type="password"
              value={newPassword}
            />
            {changePassword.error ? <ErrorState error={changePassword.error} /> : null}
            <button className="button" disabled={changePassword.isPending} type="submit">
              {changePassword.isPending ? "Changing password…" : "Change password"}
            </button>
          </form>
        </div>
      ) : (
        <div className="placeholder-card account-card">
          <dl className="status-details">
            <dt>Username</dt>
            <dd>{session?.username}</dd>
            <dt>Roles</dt>
            <dd>{session?.roles.join(", ") || "No operational roles"}</dd>
            <dt>Status</dt>
            <dd>Ready for future staff modules</dd>
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
      )}
    </section>
  );
}
