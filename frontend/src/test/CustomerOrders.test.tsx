import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { Provider } from "react-redux";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MyOrdersPage } from "../pages/MyOrdersPage";
import { createAppStore } from "../store";

const mocks = vi.hoisted(() => ({
  getMyOrders: vi.fn(),
  repeatOrder: vi.fn(),
}));

vi.mock("../api/orders", () => ({
  getMyOrders: mocks.getMyOrders,
  repeatOrder: mocks.repeatOrder,
}));
vi.mock("../hooks/useOrderNotifications", () => ({
  useOrderNotifications: () => "Connected",
}));

describe("customer order history", () => {
  beforeEach(() => {
    mocks.getMyOrders.mockReset();
    mocks.repeatOrder.mockReset();
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
    expect(screen.getAllByText("Rejected")).toHaveLength(2);
    expect(screen.getByText(/Kitchen capacity is full/)).toBeVisible();
    expect(screen.getByText(/Live updates: Connected/)).toBeVisible();
  });

  it("shows repeat validation before adding only available items", async () => {
    mocks.getMyOrders.mockResolvedValue({
      items: [
        {
          id: "completed",
          orderNumber: "MP-20260811-00003",
          status: "Completed",
          paymentMethod: "PayOnPickup",
          pickupMode: "AsSoonAsPossible",
          total: 42,
          currency: "TJS",
          itemQuantity: 2,
          createdAt: "2026-08-11T05:00:00.000Z",
          paymentReceived: true,
        },
      ],
      page: 1,
      pageSize: 12,
      totalCount: 1,
      totalPages: 1,
    });
    mocks.repeatOrder.mockResolvedValue({
      sourceOrderNumber: "MP-20260811-00003",
      availableItems: [
        {
          productId: "product-1",
          productName: "Cappuccino",
          basePrice: 22,
          unitPrice: 24,
          currency: "TJS",
          quantity: 1,
          options: [
            {
              productOptionGroupId: "group-1",
              optionGroupName: "Size",
              optionValueId: "small",
              optionValueName: "Small",
              priceModifier: 2,
            },
          ],
        },
      ],
      unavailableItems: [
        {
          productName: "Seasonal Latte",
          quantity: 1,
          reasons: ["This product is not currently available to order."],
        },
      ],
    });
    const { store } = renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Repeat order" }));
    expect(await screen.findByRole("dialog")).toBeVisible();
    expect(screen.getByText("Unavailable items")).toBeVisible();
    expect(screen.getByText(/Nothing was substituted/)).toBeVisible();
    expect(store.getState().cart.items).toHaveLength(0);

    fireEvent.click(screen.getByRole("button", { name: "Add available items to cart" }));
    expect(store.getState().cart.items).toHaveLength(1);
    expect(store.getState().cart.items[0].productName).toBe("Cappuccino");
  });

  it("sends status and product search filters to the API", async () => {
    mocks.getMyOrders.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 12,
      totalCount: 0,
      totalPages: 0,
    });
    renderPage();
    await screen.findByText("No orders yet");

    fireEvent.click(screen.getByRole("button", { name: "Active" }));
    fireEvent.change(screen.getByLabelText("Search orders"), {
      target: { value: "cappuccino" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Search" }));

    await waitFor(() => {
      expect(mocks.getMyOrders).toHaveBeenCalledWith(
        1,
        12,
        expect.any(AbortSignal),
        "Active",
        "cappuccino",
      );
    });
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const store = createAppStore(undefined);
  const rendered = render(
    <QueryClientProvider client={client}>
      <Provider store={store}>
        <MemoryRouter>
          <MyOrdersPage />
        </MemoryRouter>
      </Provider>
    </QueryClientProvider>,
  );
  return { ...rendered, store };
}
