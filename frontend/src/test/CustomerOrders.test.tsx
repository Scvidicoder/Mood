import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MyOrdersPage } from "../pages/MyOrdersPage";

const mocks = vi.hoisted(() => ({
  getMyOrders: vi.fn(),
}));

vi.mock("../api/orders", () => ({ getMyOrders: mocks.getMyOrders }));
vi.mock("../hooks/useOrderNotifications", () => ({
  useOrderNotifications: () => "Connected",
}));

describe("customer order history", () => {
  beforeEach(() => {
    mocks.getMyOrders.mockReset();
  });

  it("shows confirmed estimated time and rejected reasons", async () => {
    mocks.getMyOrders.mockResolvedValue({
      items: [
        {
          id: "confirmed",
          orderNumber: "MP-20260811-00001",
          status: "Confirmed",
          paymentMethod: "PayOnPickup",
          pickupMode: "AsSoonAsPossible",
          total: 24,
          currency: "TJS",
          itemQuantity: 1,
          createdAt: "2026-08-11T05:00:00.000Z",
          estimatedReadyAt: "2026-08-11T05:45:00.000Z",
          paymentReceived: false,
        },
        {
          id: "rejected",
          orderNumber: "MP-20260811-00002",
          status: "Rejected",
          paymentMethod: "Online",
          pickupMode: "Scheduled",
          requestedPickupTime: "2026-08-11T06:00:00.000Z",
          total: 30,
          currency: "TJS",
          itemQuantity: 2,
          createdAt: "2026-08-11T05:05:00.000Z",
          rejectReason: "Kitchen capacity is full.",
          paymentReceived: true,
        },
      ],
      page: 1,
      pageSize: 100,
      totalCount: 2,
      totalPages: 1,
    });
    renderPage();

    expect(await screen.findByText("Confirmed")).toBeVisible();
    expect(screen.getByText("Rejected")).toBeVisible();
    expect(screen.getByText(/Kitchen capacity is full/)).toBeVisible();
    expect(screen.getByText(/Live updates: Connected/)).toBeVisible();
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <MyOrdersPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
