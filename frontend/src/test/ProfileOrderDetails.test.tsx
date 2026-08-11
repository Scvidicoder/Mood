import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { OrderSuccessPage } from "../pages/OrderSuccessPage";
import { createAppStore } from "../store";

const mocks = vi.hoisted(() => ({
  getOrder: vi.fn(),
  cancelOrder: vi.fn(),
  repeatOrder: vi.fn(),
}));

vi.mock("../api/orders", () => ({
  getOrder: mocks.getOrder,
  cancelOrder: mocks.cancelOrder,
  repeatOrder: mocks.repeatOrder,
}));
vi.mock("../hooks/useOrderNotifications", () => ({
  useOrderNotifications: () => "Connected",
}));

describe("profile order details", () => {
  beforeEach(() => {
    mocks.getOrder.mockReset();
    mocks.cancelOrder.mockReset();
    mocks.repeatOrder.mockReset();
  });

  it("shows the full snapshot, payment state, dates, and highlighted timeline", async () => {
    mocks.getOrder.mockResolvedValue({
      id: "order-1",
      orderNumber: "MP-20260811-00009",
      status: "Completed",
      paymentMethod: "PayOnPickup",
      pickupMode: "AsSoonAsPossible",
      comment: "Please keep it warm",
      subtotal: 48,
      discountTotal: 0,
      total: 48,
      currency: "TJS",
      createdAt: "2026-08-11T05:00:00.000Z",
      confirmedAt: "2026-08-11T05:05:00.000Z",
      estimatedReadyAt: "2026-08-11T05:30:00.000Z",
      preparationStartedAt: "2026-08-11T05:10:00.000Z",
      readyAt: "2026-08-11T05:25:00.000Z",
      completedAt: "2026-08-11T05:35:00.000Z",
      paymentReceived: true,
      paymentMethodUsed: "Card",
      paymentReceivedAt: "2026-08-11T05:34:00.000Z",
      statusHistory: [
        { newStatus: "PendingConfirmation", timestamp: "2026-08-11T05:00:00.000Z" },
        { oldStatus: "PendingConfirmation", newStatus: "Confirmed", timestamp: "2026-08-11T05:05:00.000Z" },
        { oldStatus: "Confirmed", newStatus: "Preparing", timestamp: "2026-08-11T05:10:00.000Z" },
        { oldStatus: "Preparing", newStatus: "ReadyForPickup", timestamp: "2026-08-11T05:25:00.000Z" },
        { oldStatus: "ReadyForPickup", newStatus: "Completed", timestamp: "2026-08-11T05:35:00.000Z" },
      ],
      items: [
        {
          productId: "product-1",
          productName: "Cappuccino",
          isAvailableAtPurchase: true,
          basePrice: 22,
          finalPrice: 24,
          quantity: 2,
          volumeMilliliters: 250,
          options: [
            {
              optionGroupName: "Size",
              optionValueName: "Small",
              priceModifier: 2,
              displayOrder: 1,
            },
          ],
        },
      ],
    });
    renderPage();

    expect(await screen.findByRole("heading", { name: "Your order is complete." })).toBeVisible();
    expect(screen.getByRole("list", { name: "Order progress timeline" })).toBeVisible();
    expect(screen.getByRole("heading", { name: /Cappuccino/ })).toBeVisible();
    expect(screen.getByText("Small")).toBeVisible();
    expect(screen.getByText("Please keep it warm")).toBeVisible();
    expect(screen.getByText("Received by card")).toBeVisible();
    expect(screen.getByText(/Snapshot: base/)).toBeVisible();
    expect(screen.getByRole("link", { name: "Back to my orders" }))
      .toHaveAttribute("href", "/profile/orders");
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <Provider store={createAppStore(undefined)}>
        <MemoryRouter initialEntries={["/profile/orders/order-1"]}>
          <Routes>
            <Route path="/profile/orders/:id" element={<OrderSuccessPage />} />
          </Routes>
        </MemoryRouter>
      </Provider>
    </QueryClientProvider>,
  );
}
