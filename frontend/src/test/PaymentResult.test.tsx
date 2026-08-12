import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PaymentResultPage } from "../pages/PaymentResultPage";

const mocks = vi.hoisted(() => ({
  getPayment: vi.fn(),
  verifyPayment: vi.fn(),
}));

vi.mock("../api/payments", () => ({
  getPayment: mocks.getPayment,
  verifyPayment: mocks.verifyPayment,
}));

vi.mock("../hooks/useOrderNotifications", () => ({
  useOrderNotifications: () => "Connected",
}));

describe("payment result", () => {
  beforeEach(() => {
    mocks.getPayment.mockReset();
    mocks.verifyPayment.mockReset();
  });

  it("ignores provider-like query status and renders only backend state", async () => {
    mocks.getPayment.mockResolvedValue(payment("Failed"));
    renderResult("/payment/result?paymentId=payment-1&status=ok&paid=true");

    expect(await screen.findByRole("heading", { name: "Payment failed." })).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Payment received." })).not.toBeInTheDocument();
    expect(mocks.getPayment).toHaveBeenCalledWith("payment-1", expect.any(AbortSignal));
  });

  it("verifies a pending payment through the backend and shows the confirmed update", async () => {
    mocks.getPayment.mockResolvedValue(payment("Pending"));
    mocks.verifyPayment.mockResolvedValue({
      ...payment("Paid"),
      paidAt: "2026-08-12T05:01:00.000Z",
    });
    renderResult("/payment/result?paymentId=payment-1");

    expect(await screen.findByRole("heading", { name: "Payment received." })).toBeVisible();
    expect(mocks.verifyPayment).toHaveBeenCalledWith("payment-1");
  });

  it("does not schedule periodic verification while SignalR is connected", async () => {
    const intervalSpy = vi.spyOn(window, "setInterval");
    mocks.getPayment.mockResolvedValue(payment("Pending"));
    mocks.verifyPayment.mockResolvedValue(payment("Pending"));

    renderResult("/payment/result?paymentId=payment-1");

    await waitFor(() => expect(mocks.verifyPayment).toHaveBeenCalledTimes(1));
    expect(intervalSpy.mock.calls.some(([, delay]) => delay === 10_000)).toBe(false);
    intervalSpy.mockRestore();
  });

  it("requires a backend payment reference", () => {
    renderResult("/payment/result?status=ok");

    expect(screen.getByRole("heading", { name: "Payment reference missing." })).toBeVisible();
    expect(mocks.getPayment).not.toHaveBeenCalled();
  });
});

function renderResult(entry: string) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[entry]}>
        <PaymentResultPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function payment(status: "Pending" | "Paid" | "Failed") {
  return {
    id: "payment-1",
    orderId: "order-1",
    status,
    amount: 24,
    currency: "TJS",
    createdAt: "2026-08-12T05:00:00.000Z",
  };
}
