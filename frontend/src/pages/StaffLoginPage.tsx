import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { loginEmployee } from "../api/auth";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";

interface LoginLocationState {
  returnTo?: string;
}

export function StaffLoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { establishSession } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const login = useMutation({
    mutationFn: () => loginEmployee(username, password),
    onSuccess: (response) => {
      establishSession(response.accessToken);
      const returnTo = (location.state as LoginLocationState | null)?.returnTo;
      navigate(returnTo?.startsWith("/staff") ? returnTo : "/staff", {
        replace: true,
      });
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    login.mutate();
  }

  return (
    <section className="page auth-page">
      <div className="auth-card">
        <p className="eyebrow">Employee access</p>
        <h1>Staff sign in</h1>
        <p className="auth-copy">
          Use your assigned Mood Pickup username and password.
        </p>
        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="staff-username">Username</label>
          <input
            autoComplete="username"
            id="staff-username"
            name="username"
            onChange={(event) => setUsername(event.target.value)}
            required
            value={username}
          />
          <label htmlFor="staff-password">Password</label>
          <input
            autoComplete="current-password"
            id="staff-password"
            name="password"
            onChange={(event) => setPassword(event.target.value)}
            required
            type="password"
            value={password}
          />
          {login.error ? <ErrorState error={login.error} /> : null}
          <button className="button" disabled={login.isPending} type="submit">
            {login.isPending ? "Signing in…" : "Sign in"}
          </button>
        </form>
        <p className="auth-alternative">
          Customer? <Link to="/login">Use phone sign in</Link>
        </p>
      </div>
    </section>
  );
}
