import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../api/client";
import { accessTokenStore } from "../api/tokenStore";

describe("API client token refresh", () => {
  beforeEach(() => {
    accessTokenStore.clear();
    document.cookie =
      "__Secure-MoodPickup.Csrf=csrf-token; Secure; SameSite=Lax; Path=/";
    vi.restoreAllMocks();
  });

  it("refreshes once after a 401 and retries with the new in-memory token", async () => {
    accessTokenStore.set("expired-access-token");
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({ type: "unauthorized", title: "Authentication required" }),
          {
            status: 401,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            accessToken: "rotated-access-token",
            expiresInSeconds: 900,
          }),
          {
            status: 200,
            headers: { "Content-Type": "application/json" },
          },
        ),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ value: "secured" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );

    const response = await apiClient.get<{ value: string }>("secured-resource");

    expect(response.value).toBe("secured");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    const refreshInit = fetchMock.mock.calls[1][1] as RequestInit;
    const refreshHeaders = new Headers(refreshInit.headers);
    expect(refreshHeaders.get("X-CSRF-TOKEN")).toBe("csrf-token");
    expect(refreshInit.credentials).toBe("include");

    const retryInit = fetchMock.mock.calls[2][1] as RequestInit;
    const retryHeaders = new Headers(retryInit.headers);
    expect(retryHeaders.get("Authorization")).toBe(
      "Bearer rotated-access-token",
    );
    expect(accessTokenStore.get()).toBe("rotated-access-token");
  });

  it("keeps query parameters in the URL query instead of the route path", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ items: [] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    await apiClient.get("admin/products?includeDeleted=false&page=2");

    const requestUrl = new URL(String(fetchMock.mock.calls[0][0]));
    expect(requestUrl.pathname).toBe("/api/v1/admin/products");
    expect(requestUrl.searchParams.get("includeDeleted")).toBe("false");
    expect(requestUrl.searchParams.get("page")).toBe("2");
  });
});
