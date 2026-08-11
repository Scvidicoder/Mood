import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { KitchenDashboardPage } from "../pages/staff/kitchen/KitchenDashboardPage";
import type { KitchenOrder } from "../types/orders";

const mocks = vi.hoisted(() => ({
  roles: ["Kitchen"] as string[],
  getKitchenOrders: vi.fn(),
  startKitchenOrder: vi.fn(),
  markKitchenOrderReady: vi.fn(),
  updateKitchenOrderEta: vi.fn(),
}));

vi.mock("../api/orders", () => ({
  getKitchenOrders: mocks.getKitchenOrders,
  startKitchenOrder: mocks.startKitchenOrder,
  markKitchenOrderReady: mocks.markKitchenOrderReady,
  updateKitchenOrderEta: mocks.updateKitchenOrderEta,
}));

vi.mock("../app/AuthProvider", () => ({
  useAuth: () => ({
    session: {
      accountType: "employee",
      roles: mocks.roles,
      mustChangePassword: false,
    },
  }),
}));

describe("kitchen dashboard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.roles = ["Kitchen"];
    mocks.getKitchenOrders.mockResolvedValue({
      items: [confirmedOrder()],
      page: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
    });
  });

  it("shows immutable item details and starts preparation with the row version", async () => {
    const user = userEvent.setup();
    mocks.startKitchenOrder.mockResolvedValue({
      ...confirmedOrder(),
      status: "Preparing",
      rowVersion: "row-version-2",
    });
    renderPage();

    expect(await screen.findByText("MP-20260811-00001")).toBeVisible();
    expect(screen.getByText(/2 × Cappuccino/)).toBeVisible();
    expect(screen.getByText(/Size: Large/)).toBeVisible();
    expect(screen.getByText(/Extra hot/)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Start preparation" }));

    expect(mocks.startKitchenOrder).toHaveBeenCalledWith("order-1", {
      rowVersion: "row-version-1",
    });
    expect(await screen.findByText("Preparation started.")).toBeVisible();
  });

  it("allows cashier viewing and filtering without kitchen actions", async () => {
    const user = userEvent.setup();
    mocks.roles = ["Cashier"];
    renderPage();

    expect(await screen.findByText(/View only/)).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Start preparation" }),
    ).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText("Status"), "Preparing");

    expect(mocks.getKitchenOrders).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "Preparing" }),
      1,
      100,
      expect.any(AbortSignal),
    );
  });
});

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>
          <KitchenDashboardPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

function confirmedOrder(): KitchenOrder {
  return {
    id: "order-1",
    orderNumber: "MP-20260811-00001",
    customerName: "Amina Customer",
    customerPhoneNumber: "+992900000001",
    createdAt: "2026-08-11T05:00:00.000Z",
    pickupMode: "AsSoonAsPossible",
    estimatedReadyAt: "2026-08-11T05:45:00.000Z",
    status: "Confirmed",
    paymentMethod: "PayOnPickup",
    paymentReceived: false,
    total: 60,
    currency: "TJS",
    comment: "Pack separately",
    itemQuantity: 2,
    rowVersion: "row-version-1",
    items: [
      {
        productId: "product-1",
        productName: "Cappuccino",
        isAvailableAtPurchase: true,
        basePrice: 22,
        finalPrice: 30,
        quantity: 2,
        comment: "Extra hot",
        options: [
          {
            optionGroupName: "Size",
            optionValueName: "Large",
            priceModifier: 8,
            displayOrder: 1,
          },
        ],
      },
    ],
  };
}
