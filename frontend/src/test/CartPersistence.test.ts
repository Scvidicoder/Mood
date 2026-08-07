import { describe, expect, it, vi } from "vitest";
import {
  persistCart,
  restoreCart,
  type CartStorage,
} from "../features/cart/cartStorage";
import { cartActions } from "../features/cart/cartSlice";
import { createAppStore } from "../store";
import {
  CART_STORAGE_KEY,
  MAX_CART_QUANTITY,
} from "../types/cart";
import { cartLine } from "./cartTestFixtures";

describe("local cart persistence", () => {
  it("saves and restores a versioned whitelisted cart without media or auth data", () => {
    const storage = memoryStorage();
    const line = {
      ...cartLine(),
      imageUrl: "/media/secret/storage-key.webp",
    };
    persistCart(storage, [line]);

    const raw = storage.getItem(CART_STORAGE_KEY) ?? "";
    expect(raw).toContain('"version":1');
    expect(raw).not.toContain("storage-key");
    expect(raw).not.toContain("accessToken");
    expect(raw).not.toContain("refreshToken");
    expect(raw).not.toContain("csrf");

    const restored = restoreCart(storage);
    expect(restored.items).toHaveLength(1);
    expect(restored.items[0]).toMatchObject({
      productId: line.productId,
      quantity: 1,
      state: "checking",
    });
  });

  it("recovers from invalid JSON and an unknown schema version", () => {
    const invalid = memoryStorage({
      [CART_STORAGE_KEY]: "{not-json",
    });
    expect(restoreCart(invalid)).toMatchObject({
      items: [],
      notice: { kind: "warning" },
    });

    const unknown = memoryStorage({
      [CART_STORAGE_KEY]: JSON.stringify({
        version: 2,
        currency: "TJS",
        items: [],
      }),
    });
    expect(restoreCart(unknown).notice?.message).toMatch(
      /unsupported format/i,
    );
  });

  it("sanitizes invalid quantities and normalizes duplicate lines", () => {
    const valid = persistedLine();
    const storage = memoryStorage({
      [CART_STORAGE_KEY]: JSON.stringify({
        version: 1,
        currency: "TJS",
        updatedAt: "2026-08-06T10:00:00.000Z",
        items: [
          { ...valid, quantity: 500 },
          { ...valid, id: "duplicate", quantity: 2 },
          { ...valid, id: "invalid", quantity: 0 },
        ],
      }),
    });
    const restored = restoreCart(storage);
    expect(restored.items).toHaveLength(1);
    expect(restored.items[0].quantity).toBe(MAX_CART_QUANTITY);
    expect(restored.notice?.message).toMatch(/invalid or duplicate/i);
  });

  it("persists meaningful mutations and survives a simulated reload", () => {
    const storage = memoryStorage();
    const firstStore = createAppStore(storage);
    firstStore.dispatch(cartActions.addConfiguredLine(cartLine()));
    firstStore.dispatch(cartActions.increaseQuantity("line-1"));

    const reloadedStore = createAppStore(storage);
    expect(reloadedStore.getState().cart.items[0].quantity).toBe(2);
  });

  it("keeps the in-memory cart working when a storage write fails", () => {
    const storage: CartStorage = {
      getItem: vi.fn(() => null),
      setItem: vi.fn(() => {
        throw new DOMException("Quota exceeded", "QuotaExceededError");
      }),
    };
    const store = createAppStore(storage);
    store.dispatch(cartActions.addConfiguredLine(cartLine()));

    expect(store.getState().cart.items).toHaveLength(1);
    expect(store.getState().cart.persistenceWarning).toMatch(
      /browser storage could not be updated/i,
    );
  });
});

function persistedLine() {
  const line = cartLine();
  return {
    id: line.id,
    productId: line.productId,
    productName: line.productName,
    basePriceMinor: line.basePriceMinor,
    selectedOptions: line.selectedOptions,
    quantity: line.quantity,
    unitPriceMinor: line.unitPriceMinor,
    createdAt: line.createdAt,
    updatedAt: line.updatedAt,
  };
}

function memoryStorage(
  initial: Record<string, string> = {},
): CartStorage {
  const values = new Map(Object.entries(initial));
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
      values.set(key, value);
    },
  };
}
