import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getPayment, verifyPayment } from "../api/payments";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import type { CustomerPayment, PaymentStatus } from "../types/orders";
import { formatDate, formatMoney } from "../utils/format";
import { paymentStatusLabel } from "../utils/orderPresentation";

const maximumVerificationAttempts = 12;
const verificationIntervalMilliseconds = 10_000;

const resultCopy: Record<PaymentStatus, { heading: string; message: string }> = {
  Pending: {
    heading: "We are confirming your payment.",
    message: "Keep this page open. Status is confirmed by Mood Pickup's server, not by the return URL.",
  },
  Paid: {
    heading: "Payment received.",
    message: "Your order has been paid and is ready for the cafe's workflow.",
  },
  Failed: {
    heading: "Payment failed.",
    message: "The payment provider did not complete this payment. Your order has not been marked paid.",
  },
  Cancelled: {
    heading: "Payment cancelled.",
    message: "The payment was cancelled and your order has not been marked paid.",
  },
  RefundRequired: {
    heading: "Refund requires attention.",
    message: "The order was rejected after payment. Staff can see that a full refund is required; no unconfirmed refund is shown as completed.",
  },
  RefundPending: {
    heading: "Refund pending.",
    message: "The refund is still being confirmed.",
  },
  Refunded: {
    heading: "Payment refunded.",
    message: "The provider-confirmed refund is complete.",
  },
  ReconciliationRequired: {
    heading: "Payment needs reconciliation.",
    message: "Staff must reconcile this provider status before the payment can be treated as final.",
  },
};

export function PaymentResultPage() {
  const [searchParams] = useSearchParams();
  const paymentId = searchParams.get("paymentId") ?? "";
  const queryClient = useQueryClient();
  const verificationAttempts = useRef(0);
  const connectionState = useOrderNotifications();
  const payment = useQuery({
    queryKey: ["payments", paymentId],
    queryFn: ({ signal }) => getPayment(paymentId, signal),
    enabled: Boolean(paymentId),
    refetchInterval: (query) =>
      query.state.data?.status === "Pending" && connectionState !== "Connected"
        ? verificationIntervalMilliseconds
        : false,
  });
  const verification = useMutation({
    mutationFn: () => verifyPayment(paymentId),
    onSuccess: (updated) => {
      queryClient.setQueryData<CustomerPayment>(["payments", paymentId], updated);
      void queryClient.invalidateQueries({ queryKey: ["orders", updated.orderId] });
      void queryClient.invalidateQueries({ queryKey: ["orders", "mine"] });
    },
  });

  useEffect(() => {
    if (!paymentId || payment.data?.status !== "Pending") return;

    const verify = () => {
      if (verificationAttempts.current >= maximumVerificationAttempts) return;
      verificationAttempts.current += 1;
      verification.mutate();
    };
    verify();

    if (connectionState === "Connected") return;

    const timer = window.setInterval(verify, verificationIntervalMilliseconds);
    return () => window.clearInterval(timer);
  }, [connectionState, payment.data?.status, paymentId]);

  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Payment result - Mood Pickup";
    return () => {
      document.title = previousTitle;
    };
  }, []);

  if (!paymentId) {
    return (
      <section className="page payment-result-page">
        <div className="payment-result-card payment-result-card--error">
          <p className="eyebrow">Payment result</p>
          <h1>Payment reference missing.</h1>
          <p>Open the payment from your order history so Mood Pickup can load its server status.</p>
          <Link className="button button-link" to="/profile/orders">My orders</Link>
        </div>
      </section>
    );
  }
  if (payment.isLoading) {
    return <section className="page"><LoadingState message="Checking payment status..." /></section>;
  }
  if (payment.error || !payment.data) {
    return <section className="page"><ErrorState error={payment.error} /></section>;
  }

  const value = payment.data;
  const copy = resultCopy[value.status];
  return (
    <section className="page payment-result-page">
      <article className={`payment-result-card payment-result-card--${value.status.toLowerCase()}`}>
        <p className="eyebrow">Online payment</p>
        <span className="payment-result-status">{paymentStatusLabel(value.status)}</span>
        <h1>{copy.heading}</h1>
        <p>{copy.message}</p>
        <dl className="payment-result-facts">
          <div><dt>Amount</dt><dd>{formatMoney(value.amount, value.currency)}</dd></div>
          <div><dt>Created</dt><dd>{formatDate(value.createdAt)}</dd></div>
          {value.paidAt ? <div><dt>Paid</dt><dd>{formatDate(value.paidAt)}</dd></div> : null}
          {value.refundedAt ? <div><dt>Refunded</dt><dd>{formatDate(value.refundedAt)}</dd></div> : null}
        </dl>
        {value.failureReason ? <p className="payment-result-reason">{value.failureReason}</p> : null}
        {value.status === "Pending" ? (
          <p className="payment-result-live" role="status">
            Live updates: {connectionState}. Verification attempt {verificationAttempts.current} of {maximumVerificationAttempts}.
          </p>
        ) : null}
        {verification.error ? (
          <div className="payment-result-verification-error">
            <ErrorState error={verification.error} />
            <p>Automatic checking will retry for a limited time.</p>
          </div>
        ) : null}
        <div className="payment-result-actions">
          <Link className="button button-link" to={`/profile/orders/${value.orderId}`}>View order</Link>
          <Link className="button button-secondary button-link" to="/profile/orders">My orders</Link>
        </div>
      </article>
    </section>
  );
}
