import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  getCustomerChallengeStatus,
  requestCustomerCode,
} from "../api/auth";
import { ErrorState } from "../components/ErrorState";
import type {
  CustomerChallengeStatus,
  TelegramLoginRouteState,
} from "../types/auth";

const terminalStatuses = new Set<CustomerChallengeStatus>([
  "OtpSent",
  "Expired",
  "Locked",
  "Completed",
]);

export function TelegramLinkPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as TelegramLoginRouteState | null;
  const [expiresAt, setExpiresAt] = useState(
    () => Date.now() + (state?.expiresInSeconds ?? 0) * 1000,
  );
  const [resendAt, setResendAt] = useState(
    () => Date.now() + (state?.resendAvailableInSeconds ?? 0) * 1000,
  );
  const [, setClockTick] = useState(0);

  const statusQuery = useQuery({
    queryKey: ["customer-challenge-status", state?.challengeId],
    queryFn: () =>
      getCustomerChallengeStatus(
        state?.challengeId ?? "",
        state?.clientChallengeSecret ?? "",
      ),
    enabled: Boolean(state?.challengeId && state.clientChallengeSecret),
    retry: false,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status && terminalStatuses.has(status) ? false : 2_500;
    },
  });

  const resend = useMutation({
    mutationFn: () => requestCustomerCode(state?.phoneNumber ?? ""),
    onSuccess: (response) => {
      const nextState: TelegramLoginRouteState = {
        challengeId: response.challengeId,
        clientChallengeSecret: response.clientChallengeSecret,
        phoneNumber: state?.phoneNumber ?? "",
        telegramBotUrl: response.telegramBotUrl,
        expiresInSeconds: response.expiresInSeconds,
        resendAvailableInSeconds: response.resendAvailableInSeconds,
      };
      navigate(
        response.status === "OtpSent" ? "/verify" : "/login/telegram",
        { replace: true, state: nextState },
      );
    },
  });

  useEffect(() => {
    const interval = window.setInterval(
      () => setClockTick((value) => value + 1),
      1_000,
    );
    return () => window.clearInterval(interval);
  }, []);

  useEffect(() => {
    setExpiresAt(
      Date.now() + (state?.expiresInSeconds ?? 0) * 1000,
    );
    setResendAt(
      Date.now() +
        (state?.resendAvailableInSeconds ?? 0) * 1000,
    );
  }, [
    state?.challengeId,
    state?.expiresInSeconds,
    state?.resendAvailableInSeconds,
  ]);

  useEffect(() => {
    if (statusQuery.data) {
      setExpiresAt(Date.now() + statusQuery.data.expiresInSeconds * 1000);
    }
  }, [statusQuery.data]);

  useEffect(() => {
    if (statusQuery.data?.status === "OtpSent") {
      navigate("/verify", { replace: true, state });
    }
  }, [navigate, state, statusQuery.data?.status]);

  if (!isValidState(state)) {
    return (
      <section className="page auth-page">
        <div className="auth-card">
          <h1>Start with your phone number</h1>
          <p>
            This Telegram linking session is missing or the page was
            refreshed.
          </p>
          <Link className="button button-link" to="/login">
            Return to sign in
          </Link>
        </div>
      </section>
    );
  }

  const status =
    statusQuery.data?.status ?? "WaitingForTelegramStart";
  const secondsRemaining = Math.max(
    0,
    Math.ceil((expiresAt - Date.now()) / 1000),
  );
  const resendSeconds = Math.max(
    0,
    Math.ceil((resendAt - Date.now()) / 1000),
  );
  const canResend =
    statusQuery.data?.canResend === true || resendSeconds === 0;

  return (
    <section className="page auth-page">
      <div className="auth-card telegram-link-card">
        <p className="eyebrow">Telegram confirmation</p>
        <h1>Link your phone securely</h1>
        <ol className="telegram-steps">
          <li>Open the Mood Pickup bot using the button below.</li>
          <li>Press Start and share your own contact with Telegram.</li>
          <li>Return here when the bot sends your six-digit code.</li>
        </ol>

        <a
          className="button button-link telegram-open-button"
          href={state.telegramBotUrl}
          rel="noreferrer"
          target="_blank"
        >
          Open Telegram
        </a>
        <p className="telegram-device-hint">
          On desktop, Telegram Desktop or Telegram Web may open. On mobile,
          Telegram opens the bot directly.
        </p>

        <div className="telegram-waiting" role="status" aria-live="polite">
          <strong>{statusMessage(status)}</strong>
          <span>
            {secondsRemaining > 0
              ? `Link expires in ${formatCountdown(secondsRemaining)}.`
              : "This link has expired."}
          </span>
        </div>

        {statusQuery.error ? <ErrorState error={statusQuery.error} /> : null}
        {resend.error ? <ErrorState error={resend.error} /> : null}

        <div className="telegram-link-actions">
          <button
            className="button button-secondary"
            disabled={resend.isPending || !canResend}
            onClick={() => resend.mutate()}
            type="button"
          >
            {resend.isPending
              ? "Requesting…"
              : canResend
                ? "Request a new link"
                : `New link in ${resendSeconds}s`}
          </button>
          <button
            className="button"
            onClick={() => navigate("/verify", { state })}
            type="button"
          >
            I received the code
          </button>
        </div>
      </div>
    </section>
  );
}

function isValidState(
  state: TelegramLoginRouteState | null,
): state is TelegramLoginRouteState {
  if (
    !state?.challengeId ||
    !state.clientChallengeSecret ||
    !state.phoneNumber ||
    !state.telegramBotUrl
  ) {
    return false;
  }

  try {
    const telegramUrl = new URL(state.telegramBotUrl);
    return (
      telegramUrl.protocol === "https:" &&
      telegramUrl.hostname.toLowerCase() === "t.me"
    );
  } catch {
    return false;
  }
}

function statusMessage(status: CustomerChallengeStatus): string {
  switch (status) {
    case "WaitingForTelegramContact":
      return "The bot is waiting for your shared contact.";
    case "OtpSent":
      return "Your code was sent to Telegram.";
    case "Expired":
      return "This Telegram link expired.";
    case "Locked":
      return "This linking attempt was locked. Request a new link.";
    case "Completed":
      return "This verification challenge is complete.";
    default:
      return "Waiting for you to open Telegram.";
  }
}

function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}
