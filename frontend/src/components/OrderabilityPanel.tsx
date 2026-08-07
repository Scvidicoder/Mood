import type { Orderability } from "../types/menu";

export function OrderabilityPanel({
  orderability,
}: {
  orderability: Orderability;
}) {
  return (
    <section
      className={`orderability ${orderability.isOrderable ? "orderability--ready" : ""}`}
      aria-live="polite"
    >
      <h2>{orderability.isOrderable ? "Orderable" : "Draft — configuration incomplete"}</h2>
      {orderability.issues.length > 0 ? (
        <ul>
          {orderability.issues.map((issue, index) => (
            <li key={`${issue.code}-${index}`}>{issue.message}</li>
          ))}
        </ul>
      ) : (
        <p>The product currently satisfies all orderability rules.</p>
      )}
    </section>
  );
}
