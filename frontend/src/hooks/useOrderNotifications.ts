import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { createNotificationsConnection } from "../api/signalR";
import type { OrderDetail, OrderRealtimeEvent } from "../types/orders";
import type { ConnectionState } from "./useNotificationsConnection";

const eventNames = [
  "OrderConfirmed",
  "OrderRejected",
  "OrderPreparing",
  "EstimatedReadyTimeChanged",
  "OrderReady",
  "PaymentStatusChanged",
  "OrderCompleted",
] as const;

export function useOrderNotifications(orderId?: string): ConnectionState {
  const queryClient = useQueryClient();
  const processedEvents = useRef(new Set<string>());
  const [state, setState] = useState<ConnectionState>("Connecting");

  useEffect(() => {
    const connection = createNotificationsConnection();
    const handleUpdate = (event: OrderRealtimeEvent) => {
      if (processedEvents.current.has(event.eventId)) {
        return;
      }

      processedEvents.current.add(event.eventId);
      if (!orderId || orderId === event.entityId) {
        queryClient.setQueryData<OrderDetail>(
          ["orders", event.entityId],
          (current) =>
            current
              ? {
                  ...current,
                  status: event.status,
                  estimatedReadyAt: event.estimatedReadyAt,
                  rejectReason: event.rejectReason,
                  preparationStartedAt: event.preparationStartedAt,
                  readyAt: event.readyAt,
                  completedAt: event.completedAt,
                  paymentReceived: event.paymentReceived,
                  paymentMethodUsed: event.paymentMethodUsed,
                }
              : current,
        );
      }

      void queryClient.invalidateQueries({ queryKey: ["orders", "mine"] });
      void queryClient.invalidateQueries({ queryKey: ["orders", event.entityId] });
      void queryClient.invalidateQueries({ queryKey: ["profile"] });
    };

    eventNames.forEach((eventName) => connection.on(eventName, handleUpdate));
    connection.onreconnecting(() => setState("Reconnecting"));
    connection.onreconnected(() => {
      setState("Connected");
      void queryClient.invalidateQueries({ queryKey: ["orders"] });
      void queryClient.invalidateQueries({ queryKey: ["profile"] });
    });
    connection.onclose(() => setState("Disconnected"));
    void connection
      .start()
      .then(() => setState("Connected"))
      .catch(() => setState("Disconnected"));

    return () => {
      void connection.stop();
    };
  }, [orderId, queryClient]);

  return state;
}
