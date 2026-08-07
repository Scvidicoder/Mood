import { useEffect, useState } from "react";
import { createNotificationsConnection } from "../api/signalR";

export type ConnectionState =
  | "Connecting"
  | "Connected"
  | "Reconnecting"
  | "Disconnected";

export function useNotificationsConnection(): ConnectionState {
  const [state, setState] = useState<ConnectionState>("Connecting");

  useEffect(() => {
    const connection = createNotificationsConnection();
    connection.onreconnecting(() => setState("Reconnecting"));
    connection.onreconnected(() => setState("Connected"));
    connection.onclose(() => setState("Disconnected"));
    void connection
      .start()
      .then(() => setState("Connected"))
      .catch(() => setState("Disconnected"));

    return () => {
      void connection.stop();
    };
  }, []);

  return state;
}
