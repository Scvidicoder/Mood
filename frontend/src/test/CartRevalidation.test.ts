import { QueryClient } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { menuQueryKeys } from "../features/menu/queryKeys";
import {
  loadCartProducts,
  revalidateCartLine,
} from "../features/cart/revalidation";
import {
  cartLine,
  configurableProduct,
  productId,
  sizeGroupId,
} from "./cartTestFixtures";

describe("cart revalidation", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("refreshes changed price and display snapshots", () => {
    const line = cartLine();
    const product = configurableProduct({ name: "New Cappuccino" });
    product.optionGroups[0].values[0].priceModifier = 4;
    const outcome = revalidateCartLine(
      line,
      { kind: "found", product },
      "2026-08-06T11:00:00.000Z",
    );
    expect(outcome.state).toBe("updated");
    expect(outcome.refreshedLine).toMatchObject({
      productName: "New Cappuccino",
      unitPriceMinor: 2600,
    });
    expect(outcome.messages.join(" ")).toMatch(/price changed/i);
  });

  it("keeps removed, unavailable, and non-orderable products visible with guidance", () => {
    expect(
      revalidateCartLine(cartLine(), { kind: "missing" }),
    ).toMatchObject({ state: "unavailable" });
    expect(
      revalidateCartLine(cartLine(), {
        kind: "found",
        product: configurableProduct({ isAvailable: false }),
      }),
    ).toMatchObject({ state: "unavailable" });
    expect(
      revalidateCartLine(cartLine(), {
        kind: "found",
        product: configurableProduct({
          isOrderable: false,
          availabilityIssues: [
            {
              code: "REQUIRED_OPTION_HAS_NO_AVAILABLE_VALUES",
              message: "Required options are unavailable.",
            },
          ],
        }),
      }),
    ).toMatchObject({
      state: "needsAttention",
      messages: expect.arrayContaining(["Required options are unavailable."]),
    });
  });

  it("detects removed/unavailable options and changed constraints", () => {
    const removed = configurableProduct();
    removed.optionGroups[0].values = [];
    expect(
      revalidateCartLine(cartLine(), { kind: "found", product: removed }),
    ).toMatchObject({ state: "needsAttention" });

    const unavailable = configurableProduct();
    unavailable.optionGroups[0].values[0].isAvailable = false;
    expect(
      revalidateCartLine(cartLine(), {
        kind: "found",
        product: unavailable,
      }),
    ).toMatchObject({ state: "needsAttention" });

    const changed = configurableProduct();
    changed.optionGroups[0] = {
      ...changed.optionGroups[0],
      selectionType: "Multiple",
      minimumSelections: 2,
      maximumSelections: 2,
    };
    expect(
      revalidateCartLine(cartLine(), { kind: "found", product: changed }),
    ).toMatchObject({ state: "needsAttention" });
  });

  it("reports an offline or intermittent lookup as needs attention", () => {
    expect(
      revalidateCartLine(cartLine(), { kind: "unverified" }),
    ).toMatchObject({
      state: "needsAttention",
      messages: [expect.stringMatching(/could not check/i)],
    });
  });

  it("deduplicates product IDs and reuses cached detail data", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    queryClient.setQueryData(
      menuQueryKeys.publicProduct(productId),
      configurableProduct(),
    );
    const fetchMock = vi.spyOn(globalThis, "fetch");

    const results = await loadCartProducts(queryClient, [
      productId,
      productId,
    ]);

    expect(results[productId].kind).toBe("found");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("bounds a larger cart fixture to four concurrent detail requests", async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    let active = 0;
    let maximumActive = 0;
    const ids = Array.from({ length: 12 }, (_, index) => `product-${index}`);
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      active += 1;
      maximumActive = Math.max(maximumActive, active);
      await new Promise((resolve) => window.setTimeout(resolve, 2));
      active -= 1;
      const id = String(input).split("/").at(-1) ?? productId;
      const product = configurableProduct({
        id,
        optionGroups: configurableProduct().optionGroups.map((group) => ({
          ...group,
          id: group.id === sizeGroupId ? `${group.id}-${id}` : group.id,
        })),
      });
      return json(product);
    });

    const results = await loadCartProducts(queryClient, ids);

    expect(Object.keys(results)).toHaveLength(12);
    expect(maximumActive).toBeLessThanOrEqual(4);
  });
});

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}
