import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useOrderNotifications } from "../hooks/useOrderNotifications";
import type { OrderDetail, OrderRealtimeEvent } from "../types/orders";

const signalR = vi.hoisted(() => {
  const handlers: Record<string, (event: OrderRealtimeEvent) => void> = {};
  return {
    handlers,
    connection: {
      on: vi.fn((name: string, handler: (event: OrderRealtimeEvent) => void) => {
        handlers[name] = handler;
      }),
      onreconnecting: vi.fn(),
      onreconnected: vi.fn(),
      onclose: vi.fn(),
      start: vi.fn(() => Promise.resolve()),
      stop: vi.fn(() => Promise.resolve()),
    },
  };
});

vi.mock("../api/signalR", () => ({
  createNotificationsConnection: () => signalR.connection,
}));

describe("customer order SignalR updates", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(signalR.handlers).forEach((key) => delete signalR.handlers[key]);
  });

  it("updates the cached order immediately and ignores duplicate event IDs", async () => {
    const client = new QueryClient();
    client.setQueryData<OrderDetail>(["orders", "order-1"], pendingOrder());
    render(
      <QueryClientProvider client={client}>
        <ConnectionProbe />
      </QueryClientProvider>,
    );
    expect(await screen.findByText("Connected")).toBeVisible();

    const event: OrderRealtimeEvent = {
      eventId: "event-1",
      timestamp: "2026-08-11T05:01:00.000Z",
      entityId: "order-1",
      orderNumber: "MP-20260811-00001",
      status: "Confirmed",
      estimatedReadyAt: "2026-08-11T05:45:00.000Z",
    };
    act(() => signalR.handlers.OrderConfirmed(event));
    expect(client.getQueryData<OrderDetail>(["orders", "order-1"])?.status)
      .toBe("Confirmed");

    act(() => signalR.handlers.OrderRejected({
      ...event,
      status: "Rejected",
      rejectReason: "Duplicate should be ignored",
    }));
    expect(client.getQueryData<OrderDetail>(["orders", "order-1"])?.status)
      .toBe("Confirmed");
  });
});

function ConnectionProbe() {
  const state = useOrderNotifications("order-1");
  return <span>{state}</span>;
}

function pendingOrder(): OrderDetail {
  return {
    id: "order-1",
    orderNumber: "MP-20260811-00001",
    status: "PendingConfirmation",
    paymentMethod: "PayOnPickup",
    pickupMode: "AsSoonAsPossible",
    subtotal: 24,
    discountTotal: 0,
    total: 24,
    currency: "TJS",
    createdAt: "2026-08-11T05:00:00.000Z",
    items: [],
  };
}
