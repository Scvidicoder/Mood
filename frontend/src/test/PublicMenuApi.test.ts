import { beforeEach, describe, expect, it, vi } from "vitest";
import { getPublicProducts } from "../api/menu/publicMenu";

describe("public menu API", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("loads every projected page for a large customer menu", async () => {
    const firstPageItems = Array.from({ length: 100 }, (_, index) =>
      product(`product-${index}`),
    );
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input) => {
        const url = new URL(String(input));
        const page = Number(url.searchParams.get("page"));
        return json({
          items: page === 1 ? firstPageItems : [product("product-100")],
          page,
          pageSize: 100,
          totalCount: 101,
          totalPages: 2,
        });
      },
    );

    const result = await getPublicProducts({
      categoryId: "11111111-1111-1111-1111-111111111111",
      search: "coffee",
    });

    expect(result).toHaveLength(101);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls.map(([input]) => String(input))).toEqual([
      expect.stringMatching(/categoryId=11111111-1111-1111-1111-111111111111/),
      expect.stringMatching(/page=2/),
    ]);
    expect(String(fetchMock.mock.calls[0][0])).toContain("search=coffee");
    expect(String(fetchMock.mock.calls[0][0])).toContain("pageSize=100");
  });
});

function product(id: string) {
  return {
    id,
    categoryId: "11111111-1111-1111-1111-111111111111",
    name: id,
    shortDescription: "Coffee",
    imageUrl: null,
    priceFrom: 20,
    currency: "TJS",
    weightGrams: null,
    volumeMilliliters: 250,
    calories: 10,
    isAvailable: true,
    isOrderable: true,
    availabilityIssues: [],
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
