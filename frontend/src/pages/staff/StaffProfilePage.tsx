import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { changeEmployeePassword } from "../../api/auth";
import { useAuth } from "../../app/AuthProvider";
import { ErrorState } from "../../components/ErrorState";
import { useToast } from "../../components/ToastProvider";

export function StaffProfilePage() {
  const { session, refreshSession } = useAuth();
  const { notify } = useToast();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const changePassword = useMutation({
    mutationFn: () => changeEmployeePassword(currentPassword, newPassword),
    onSuccess: async () => {
      setCurrentPassword("");
      setNewPassword("");
      await refreshSession();
      notify("Password changed successfully.");
    },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    changePassword.mutate();
  }

  return (
    <section>
      <div className="staff-page-heading">
        <div>
          <p className="eyebrow">Account</p>
          <h1>Staff profile</h1>
          <p>Your identity and assigned authorization roles.</p>
        </div>
      </div>
      <div className="two-column-grid">
        <section className="panel">
          <h2>Profile</h2>
          <dl className="status-details">
            <dt>Name</dt>
            <dd>{session?.fullName || "Not supplied"}</dd>
            <dt>Username</dt>
            <dd>{session?.username}</dd>
            <dt>Roles</dt>
            <dd>{session?.roles.join(", ") || "No roles"}</dd>
          </dl>
        </section>
        <section className="panel">
          <h2>Change password</h2>
          <form className="form-grid" onSubmit={submit}>
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
              {changePassword.isPending ? "Saving…" : "Change password"}
            </button>
          </form>
        </section>
      </div>
    </section>
  );
}
