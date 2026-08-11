import {
  CART_CURRENCY,
  CART_SCHEMA_VERSION,
  CART_STORAGE_KEY,
  MAX_CART_LINES,
  MAX_CART_QUANTITY,
  MAX_CART_UNIT_PRICE_MINOR,
  type CartLine,
  type CartNotice,
  type PersistedCartLineV1,
  type PersistedCartOptionV1,
  type PersistedCartV1,
} from "../../types/cart";
import { createConfigurationKey } from "./configuration";

export interface CartStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export interface RestoredCart {
  items: CartLine[];
  notice?: CartNotice;
}

export function browserCartStorage(): CartStorage | undefined {
  try {
    return window.localStorage;
  } catch {
    return undefined;
  }
}

export function restoreCart(storage?: CartStorage): RestoredCart {
  if (!storage) {
    return {
      items: [],
      notice: {
        kind: "warning",
        message:
          "Browser storage is unavailable. Your cart will work for this visit but may not survive a refresh.",
      },
    };
  }

  let raw: string | null;
  try {
    raw = storage.getItem(CART_STORAGE_KEY);
  } catch {
    return {
      items: [],
      notice: {
        kind: "warning",
        message:
          "The saved cart could not be read. A new in-memory cart is ready.",
      },
    };
  }

  if (!raw) {
    return { items: [] };
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return resetNotice(
      "The saved cart was damaged and could not be restored. A new cart is ready.",
    );
  }

  if (!isRecord(parsed) || parsed.version !== CART_SCHEMA_VERSION) {
    return resetNotice(
      "The saved cart uses an unsupported format. A new cart is ready.",
    );
  }
  if (parsed.currency !== CART_CURRENCY) {
    return resetNotice(
      `The saved cart used an unsupported currency. A new ${CART_CURRENCY} cart is ready.`,
    );
  }
  if (!Array.isArray(parsed.items)) {
    return resetNotice(
      "The saved cart was incomplete and could not be restored. A new cart is ready.",
    );
  }

  let discarded = 0;
  let normalized = 0;
  const linesByKey = new Map<string, CartLine>();

  for (const candidate of parsed.items.slice(0, MAX_CART_LINES * 2)) {
    const line = sanitizeLine(candidate);
    if (!line) {
      discarded += 1;
      continue;
    }

    const existing = linesByKey.get(line.configurationKey);
    if (existing) {
      existing.quantity = Math.min(
        MAX_CART_QUANTITY,
        existing.quantity + line.quantity,
      );
      existing.updatedAt =
        existing.updatedAt > line.updatedAt
          ? existing.updatedAt
          : line.updatedAt;
      normalized += 1;
      continue;
    }

    if (linesByKey.size >= MAX_CART_LINES) {
      discarded += 1;
      continue;
    }
    linesByKey.set(line.configurationKey, line);
  }

  const items = [...linesByKey.values()];
  return {
    items,
    notice:
      discarded > 0 || normalized > 0
        ? {
            kind: "warning",
            message:
              "The saved cart contained invalid or duplicate entries. Valid items were restored and normalized.",
          }
        : undefined,
  };
}

export function persistCart(
  storage: CartStorage,
  items: readonly CartLine[],
): void {
  const persisted: PersistedCartV1 = {
    version: CART_SCHEMA_VERSION,
    currency: CART_CURRENCY,
    updatedAt: new Date().toISOString(),
    items: items.map(toPersistedLine),
  };
  storage.setItem(CART_STORAGE_KEY, JSON.stringify(persisted));
}

export function clearBrowserCartStorage(): void {
  try {
    window.localStorage.removeItem(CART_STORAGE_KEY);
  } catch {
    // The Redux cart is still cleared even when browser storage is unavailable.
  }
}

export function toPersistedLine(line: CartLine): PersistedCartLineV1 {
  return {
    id: line.id,
    productId: line.productId,
    productName: line.productName,
    basePriceMinor: line.basePriceMinor,
    selectedOptions: line.selectedOptions.map((option) => ({
      groupId: option.groupId,
      groupName: option.groupName,
      optionValueId: option.optionValueId,
      valueName: option.valueName,
      priceModifierMinor: option.priceModifierMinor,
      volumeMilliliters: option.volumeMilliliters,
      calories: option.calories,
    })),
    quantity: line.quantity,
    unitPriceMinor: line.unitPriceMinor,
    createdAt: line.createdAt,
    updatedAt: line.updatedAt,
  };
}

function sanitizeLine(value: unknown): CartLine | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const productId = safeString(value.productId, 100);
  const productName = safeString(value.productName, 160);
  const id = safeString(value.id, 120);
  const basePriceMinor = safeMinor(value.basePriceMinor);
  const unitPriceMinor = safeMinor(value.unitPriceMinor);
  const quantity = safeQuantity(value.quantity);
  const createdAt = safeTimestamp(value.createdAt);
  const updatedAt = safeTimestamp(value.updatedAt);
  if (
    !productId ||
    !productName ||
    !id ||
    basePriceMinor == null ||
    unitPriceMinor == null ||
    quantity == null ||
    !createdAt ||
    !updatedAt ||
    !Array.isArray(value.selectedOptions)
  ) {
    return undefined;
  }

  const selectedOptions: PersistedCartOptionV1[] = [];
  const seenOptionIds = new Set<string>();
  for (const candidate of value.selectedOptions) {
    const option = sanitizeOption(candidate);
    if (!option || seenOptionIds.has(option.optionValueId)) {
      return undefined;
    }
    seenOptionIds.add(option.optionValueId);
    selectedOptions.push(option);
  }

  return {
    id,
    configurationKey: createConfigurationKey(
      productId,
      selectedOptions.map((option) => option.optionValueId),
    ),
    productId,
    productName,
    basePriceMinor,
    currency: CART_CURRENCY,
    selectedOptions,
    quantity,
    unitPriceMinor,
    createdAt,
    updatedAt,
    state: "checking",
    messages: ["Checking this item against the current menu."],
  };
}

function sanitizeOption(value: unknown): PersistedCartOptionV1 | undefined {
  if (!isRecord(value)) {
    return undefined;
  }
  const groupId = safeString(value.groupId, 100);
  const groupName = safeString(value.groupName, 120);
  const optionValueId = safeString(value.optionValueId, 100);
  const valueName = safeString(value.valueName, 120);
  const priceModifierMinor = safeMinor(value.priceModifierMinor);
  if (
    !groupId ||
    !groupName ||
    !optionValueId ||
    !valueName ||
    priceModifierMinor == null
  ) {
    return undefined;
  }

  return {
    groupId,
    groupName,
    optionValueId,
    valueName,
    priceModifierMinor,
    volumeMilliliters: safeOptionalNonNegativeInteger(
      value.volumeMilliliters,
    ),
    calories: safeOptionalNonNegativeInteger(value.calories),
  };
}

function resetNotice(message: string): RestoredCart {
  return { items: [], notice: { kind: "warning", message } };
}

function safeString(value: unknown, maximumLength: number): string | undefined {
  return typeof value === "string" &&
    value.trim() === value &&
    value.length > 0 &&
    value.length <= maximumLength
    ? value
    : undefined;
}

function safeMinor(value: unknown): number | undefined {
  return typeof value === "number" &&
    Number.isSafeInteger(value) &&
    value >= 0 &&
    value <= MAX_CART_UNIT_PRICE_MINOR
    ? value
    : undefined;
}

function safeQuantity(value: unknown): number | undefined {
  return typeof value === "number" &&
    Number.isInteger(value) &&
    value > 0
    ? Math.min(value, MAX_CART_QUANTITY)
    : undefined;
}

function safeTimestamp(value: unknown): string | undefined {
  return typeof value === "string" && Number.isFinite(Date.parse(value))
    ? new Date(value).toISOString()
    : undefined;
}

function safeOptionalNonNegativeInteger(
  value: unknown,
): number | undefined {
  return value == null
    ? undefined
    : typeof value === "number" &&
        Number.isInteger(value) &&
        value >= 0
      ? value
      : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
