import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { verifyCustomerCode } from "../api/auth";
import { useAuth } from "../app/AuthProvider";
import { ErrorState } from "../components/ErrorState";
import type { TelegramLoginRouteState } from "../types/auth";

export function VerifyCodePage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { establishSession } = useAuth();
  const state = location.state as TelegramLoginRouteState | null;
  const [code, setCode] = useState("");
  const verifyCode = useMutation({
    mutationFn: () =>
      verifyCustomerCode(state?.challengeId ?? "", code),
    onSuccess: (response) => {
      if (response.isNewCustomer && response.registrationToken) {
        navigate("/register", {
          replace: true,
          state: { registrationToken: response.registrationToken },
        });
        return;
      }

      if (!response.accessToken) {
        throw new Error("The authentication response did not include an access token.");
      }

      establishSession(response.accessToken);
      navigate("/profile", { replace: true });
    },
  });

  if (!state?.challengeId) {
    return (
      <section className="page auth-page">
        <div className="auth-card">
          <h1>Start with your phone number</h1>
          <p>This verification session is missing or has been refreshed.</p>
          <Link className="button button-link" to="/login">
            Return to sign in
          </Link>
        </div>
      </section>
    );
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    verifyCode.mutate();
  }

  return (
    <section className="page auth-page">
      <div className="auth-card">
        <p className="eyebrow">Telegram verification</p>
        <h1>Enter your code</h1>
        <p className="auth-copy">
          Enter the six-digit code sent to Telegram for{" "}
          <strong>{state.phoneNumber}</strong>. In Development fake mode, the
          code appears only in the backend’s structured logs.
        </p>
        <a
          className="telegram-link"
          href={state.telegramBotUrl}
          rel="noreferrer"
          target="_blank"
        >
          Open Telegram bot
        </a>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="verification-code">Verification code</label>
          <input
            autoComplete="one-time-code"
            id="verification-code"
            inputMode="numeric"
            maxLength={6}
            name="code"
            onChange={(event) => setCode(event.target.value)}
            pattern="[0-9]{6}"
            placeholder="000000"
            required
            value={code}
          />
          {verifyCode.error ? <ErrorState error={verifyCode.error} /> : null}
          <button className="button" disabled={verifyCode.isPending} type="submit">
            {verifyCode.isPending ? "Verifying…" : "Verify code"}
          </button>
        </form>
      </div>
    </section>
  );
}
