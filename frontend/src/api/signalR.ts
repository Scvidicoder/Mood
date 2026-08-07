import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { environment } from "../utils/environment";
import { accessTokenStore } from "./tokenStore";

export function createNotificationsConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(environment.signalRUrl, {
      accessTokenFactory: () => accessTokenStore.get() ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
