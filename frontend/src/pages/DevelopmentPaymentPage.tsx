import { useMutation, useQuery } from "@tanstack/react-query";
import { Navigate, useNavigate, useParams } from "react-router-dom";
import { healthApi } from "../api/health";
import { getOrder } from "../api/orders";
import {
  getPayment,
  simulateDevelopmentPayment,
  type DevelopmentPaymentOutcome,
} from "../api/payments";
import { ErrorState } from "../components/ErrorState";
import { LoadingState } from "../components/LoadingState";
import { formatMoney } from "../utils/format";

export function DevelopmentPaymentPage() {
  const paymentId = useParams().paymentId ?? "";
  const navigate = useNavigate();
  const systemInfo = useQuery({
    queryKey: ["system", "info"],
    queryFn: ({ signal }) => healthApi.getSystemInfo(signal),
  });
  const isDevelopment = systemInfo.data?.environment === "Development";
  const payment = useQuery({
    queryKey: ["payments", paymentId],
    queryFn: ({ signal }) => getPayment(paymentId, signal),
    enabled: isDevelopment && Boolean(paymentId),
  });
  const order = useQuery({
    queryKey: ["orders", payment.data?.orderId],
    queryFn: ({ signal }) => getOrder(payment.data!.orderId, signal),
    enabled: Boolean(payment.data?.orderId),
  });
  const simulation = useMutation({
    mutationFn: (outcome: DevelopmentPaymentOutcome) =>
      simulateDevelopmentPayment(paymentId, outcome),
    onSuccess: () => {
      navigate(`/payment/result?paymentId=${encodeURIComponent(paymentId)}`, {
        replace: true,
      });
    },
  });

  if (systemInfo.isLoading || payment.isLoading || order.isLoading) {
    return <section className="page"><LoadingState message="Loading payment..." /></section>;
  }
  if (systemInfo.error) {
    return <section className="page"><ErrorState error={systemInfo.error} /></section>;
  }
  if (!isDevelopment) {
    return <Navigate replace to="/" />;
  }
  if (payment.error || order.error || !payment.data || !order.data) {
    return <section className="page"><ErrorState error={payment.error ?? order.error} /></section>;
  }

  return (
    <section className="page payment-result-page">
      <article className="payment-result-card">
        <dl className="payment-result-facts">
          <div><dt>Order</dt><dd>{order.data.orderNumber}</dd></div>
          <div><dt>Amount</dt><dd>{formatMoney(payment.data.amount, payment.data.currency)}</dd></div>
        </dl>
        {simulation.error ? <ErrorState error={simulation.error} /> : null}
        <div className="payment-result-actions">
          <button className="button" disabled={simulation.isPending} onClick={() => simulation.mutate("success")} type="button">Success</button>
          <button className="button" disabled={simulation.isPending} onClick={() => simulation.mutate("failed")} type="button">Failed</button>
          <button className="button" disabled={simulation.isPending} onClick={() => simulation.mutate("cancel")} type="button">Cancel</button>
          <button className="button" disabled={simulation.isPending} onClick={() => simulation.mutate("pending")} type="button">Pending</button>
        </div>
      </article>
    </section>
  );
}
