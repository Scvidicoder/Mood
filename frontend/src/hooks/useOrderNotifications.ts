import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { createNotificationsConnection } from "../api/signalR";
import type { OrderDetail, OrderRealtimeEvent } from "../types/orders";
import type { ConnectionState } from "./useNotificationsConnection";

const eventNames = [
  "OrderConfirmed",
  "OrderRejected",
  "EstimatedReadyTimeChanged",
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
                }
              : current,
        );
      }

      void queryClient.invalidateQueries({ queryKey: ["orders", "mine"] });
    };

    eventNames.forEach((eventName) => connection.on(eventName, handleUpdate));
    connection.onreconnecting(() => setState("Reconnecting"));
    connection.onreconnected(() => {
      setState("Connected");
      void queryClient.invalidateQueries({ queryKey: ["orders"] });
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
