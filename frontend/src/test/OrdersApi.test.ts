import { beforeEach, describe, expect, it, vi } from "vitest";
import { getStaffOrders } from "../api/orders";

describe("staff order API filters", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("omits status for All and sends explicit workflow statuses", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async () => new Response(JSON.stringify({
          items: [],
          page: 1,
          pageSize: 100,
          totalCount: 0,
          totalPages: 0,
        }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
    );

    await getStaffOrders(undefined, 1, 100);
    await getStaffOrders("ReadyForPickup", 1, 100);

    const allUrl = new URL(String(fetchMock.mock.calls[0][0]));
    const readyUrl = new URL(String(fetchMock.mock.calls[1][0]));
    expect(allUrl.searchParams.has("status")).toBe(false);
    expect(readyUrl.searchParams.get("status")).toBe("ReadyForPickup");
  });
});
