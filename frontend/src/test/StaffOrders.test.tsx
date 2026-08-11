import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { StaffOrdersPage } from "../pages/staff/orders/StaffOrdersPage";
import type { StaffOrderDetail, StaffOrderSummary } from "../types/orders";

const mocks = vi.hoisted(() => ({
  getStaffOrders: vi.fn(),
  confirmOrder: vi.fn(),
  rejectOrder: vi.fn(),
  updateEstimatedReadyTime: vi.fn(),
}));

vi.mock("../api/orders", () => mocks);

describe("staff order dashboard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getStaffOrders.mockResolvedValue({
      items: [pendingOrder()],
      page: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
    });
  });

  it("renders pending customer details and confirms with the current row version", async () => {
    const user = userEvent.setup();
    mocks.confirmOrder.mockResolvedValue(detail({ status: "Confirmed" }));
    renderPage();

    expect(await screen.findByText("MP-20260811-00001")).toBeVisible();
    expect(screen.getByText("Amina Customer")).toBeVisible();
    expect(screen.getByText("+992900000001")).toBeVisible();
    expect(screen.getByText("Please call on arrival.")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Confirm" }));
    fireEvent.change(screen.getByLabelText("Estimated ready time"), {
      target: { value: "2026-08-11T11:30" },
    });
    await user.click(screen.getByRole("button", { name: "Confirm order" }));

    expect(mocks.confirmOrder).toHaveBeenCalledWith(
      "order-1",
      expect.objectContaining({
        rowVersion: "row-version-1",
        estimatedReadyTime: expect.any(String),
      }),
    );
    expect(await screen.findByText("Order confirmed.")).toBeVisible();
  });

  it("requires and submits a rejection reason", async () => {
    const user = userEvent.setup();
    mocks.rejectOrder.mockResolvedValue(
      detail({ status: "Rejected", rejectReason: "Capacity is full." }),
    );
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Reject" }));
    await user.type(screen.getByLabelText("Reason"), "Capacity is full.");
    await user.click(screen.getByRole("button", { name: "Reject order" }));

    expect(mocks.rejectOrder).toHaveBeenCalledWith("order-1", {
      reason: "Capacity is full.",
      rowVersion: "row-version-1",
    });
    expect(await screen.findByText("Order rejected.")).toBeVisible();
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
          <StaffOrdersPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

function pendingOrder(): StaffOrderSummary {
  return {
    id: "order-1",
    orderNumber: "MP-20260811-00001",
    customerName: "Amina Customer",
    customerPhoneNumber: "+992900000001",
    createdAt: "2026-08-11T05:00:00.000Z",
    pickupMode: "AsSoonAsPossible",
    paymentMethod: "PayOnPickup",
    total: 24,
    currency: "TJS",
    comment: "Please call on arrival.",
    status: "PendingConfirmation",
    itemQuantity: 2,
    rowVersion: "row-version-1",
  };
}

function detail(overrides: Partial<StaffOrderDetail>): StaffOrderDetail {
  return {
    ...pendingOrder(),
    subtotal: 24,
    discountTotal: 0,
    items: [],
    ...overrides,
  };
}
