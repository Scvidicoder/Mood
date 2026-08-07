import { environment } from "./environment";

const apiOrigin = new URL(environment.apiUrl, window.location.origin).origin;

export function resolveMediaUrl(url: string | null | undefined): string | null {
  return url ? new URL(url, apiOrigin).toString() : null;
}
