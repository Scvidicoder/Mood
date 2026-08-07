import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { requestCustomerCode } from "../api/auth";
import { ErrorState } from "../components/ErrorState";
import type { TelegramLoginRouteState } from "../types/auth";

export function CustomerLoginPage() {
  const navigate = useNavigate();
  const [phoneNumber, setPhoneNumber] = useState("+992");
  const requestCode = useMutation({
    mutationFn: () => requestCustomerCode(phoneNumber),
    onSuccess: (response) => {
      const routeState: TelegramLoginRouteState = {
        challengeId: response.challengeId,
        clientChallengeSecret: response.clientChallengeSecret,
        phoneNumber,
        telegramBotUrl: response.telegramBotUrl,
        expiresInSeconds: response.expiresInSeconds,
        resendAvailableInSeconds: response.resendAvailableInSeconds,
      };
      navigate(
        response.status === "OtpSent" ? "/verify" : "/login/telegram",
        { state: routeState },
      );
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    requestCode.mutate();
  }

  return (
    <section className="page auth-page">
      <div className="auth-card">
        <p className="eyebrow">Customer sign in</p>
        <h1>Continue with your phone</h1>
        <p className="auth-copy">
          We’ll confirm your phone through the Mood Pickup Telegram bot and
          send a six-digit verification code. Customer accounts never use
          passwords.
        </p>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="phone-number">Phone number</label>
          <input
            autoComplete="tel"
            id="phone-number"
            inputMode="tel"
            name="phoneNumber"
            onChange={(event) => setPhoneNumber(event.target.value)}
            placeholder="+992900000000"
            required
            value={phoneNumber}
          />
          {requestCode.error ? <ErrorState error={requestCode.error} /> : null}
          <button className="button" disabled={requestCode.isPending} type="submit">
            {requestCode.isPending ? "Requesting code…" : "Request Telegram code"}
          </button>
        </form>

        <p className="auth-alternative">
          Mood employee? <Link to="/staff/login">Use staff sign in</Link>
        </p>
      </div>
    </section>
  );
}
