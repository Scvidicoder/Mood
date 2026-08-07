import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { completeCustomerRegistration } from "../api/auth";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";

interface RegistrationLocationState {
  registrationToken: string;
}

export function CustomerRegistrationPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { establishSession } = useAuth();
  const state = location.state as RegistrationLocationState | null;
  const [name, setName] = useState("");
  const registration = useMutation({
    mutationFn: () =>
      completeCustomerRegistration(state?.registrationToken ?? "", name),
    onSuccess: (response) => {
      establishSession(response.accessToken);
      navigate("/profile", { replace: true });
    },
  });

  if (!state?.registrationToken) {
    return (
      <section className="page auth-page">
        <div className="auth-card">
          <h1>Verify your phone first</h1>
          <p>A valid phone-verification session is required for registration.</p>
          <Link className="button button-link" to="/login">
            Start registration
          </Link>
        </div>
      </section>
    );
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    registration.mutate();
  }

  return (
    <section className="page auth-page">
      <div className="auth-card">
        <p className="eyebrow">One last step</p>
        <h1>What should we call you?</h1>
        <p className="auth-copy">
          Your phone is verified. Add the name you want Mood Pickup to use.
        </p>
        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="customer-name">Name</label>
          <input
            autoComplete="name"
            id="customer-name"
            maxLength={100}
            name="name"
            onChange={(event) => setName(event.target.value)}
            required
            value={name}
          />
          {registration.error ? <ErrorState error={registration.error} /> : null}
          <button className="button" disabled={registration.isPending} type="submit">
            {registration.isPending ? "Creating profile…" : "Create profile"}
          </button>
        </form>
      </div>
    </section>
  );
}
