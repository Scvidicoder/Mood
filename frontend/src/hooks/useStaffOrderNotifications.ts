import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { createNotificationsConnection } from "../api/signalR";
import type { OrderRealtimeEvent } from "../types/orders";
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

export function useStaffOrderNotifications(): ConnectionState {
  const queryClient = useQueryClient();
  const processedEvents = useRef(new Set<string>());
  const [state, setState] = useState<ConnectionState>("Connecting");

  useEffect(() => {
    const connection = createNotificationsConnection();
    const handleUpdate = (event: OrderRealtimeEvent) => {
      if (processedEvents.current.has(event.eventId)) return;
      processedEvents.current.add(event.eventId);
      void queryClient.invalidateQueries({ queryKey: ["staff", "orders"] });
      void queryClient.invalidateQueries({ queryKey: ["staff", "kitchen"] });
    };

    eventNames.forEach((eventName) => connection.on(eventName, handleUpdate));
    connection.onreconnecting(() => setState("Reconnecting"));
    connection.onreconnected(() => {
      setState("Connected");
      void queryClient.invalidateQueries({ queryKey: ["staff"] });
    });
    connection.onclose(() => setState("Disconnected"));
    void connection
      .start()
      .then(() => setState("Connected"))
      .catch(() => setState("Disconnected"));

    return () => {
      void connection.stop();
    };
  }, [queryClient]);

  return state;
}
