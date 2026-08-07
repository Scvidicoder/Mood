export const CART_CURRENCY = "TJS";
export const CART_SCHEMA_VERSION = 1;
export const CART_STORAGE_KEY = "moodpickup.cart.v1";
export const MAX_CART_QUANTITY = 99;
export const MAX_CART_LINES = 100;
export const MAX_CART_UNIT_PRICE_MINOR = Math.floor(
  Number.MAX_SAFE_INTEGER / MAX_CART_QUANTITY / MAX_CART_LINES,
);

export type CartLineState =
  | "valid"
  | "updated"
  | "needsAttention"
  | "unavailable"
  | "checking";

export interface CartOptionSnapshot {
  groupId: string;
  groupName: string;
  optionValueId: string;
  valueName: string;
  priceModifierMinor: number;
  volumeMilliliters?: number;
  calories?: number;
}

export interface CartLine {
  id: string;
  configurationKey: string;
  productId: string;
  productName: string;
  imageUrl?: string;
  basePriceMinor: number;
  currency: typeof CART_CURRENCY;
  selectedOptions: CartOptionSnapshot[];
  quantity: number;
  unitPriceMinor: number;
  createdAt: string;
  updatedAt: string;
  state: CartLineState;
  messages: string[];
}

export interface CartNotice {
  kind: "info" | "warning";
  message: string;
}

export interface CartState {
  items: CartLine[];
  restorationNotice?: CartNotice;
  persistenceWarning?: string;
  announcement?: string;
}

export interface PersistedCartOptionV1 {
  groupId: string;
  groupName: string;
  optionValueId: string;
  valueName: string;
  priceModifierMinor: number;
  volumeMilliliters?: number;
  calories?: number;
}

export interface PersistedCartLineV1 {
  id: string;
  productId: string;
  productName: string;
  basePriceMinor: number;
  selectedOptions: PersistedCartOptionV1[];
  quantity: number;
  unitPriceMinor: number;
  createdAt: string;
  updatedAt: string;
}

export interface PersistedCartV1 {
  version: typeof CART_SCHEMA_VERSION;
  currency: typeof CART_CURRENCY;
  updatedAt: string;
  items: PersistedCartLineV1[];
}

export interface CartLineRevalidation {
  lineId: string;
  state: Exclude<CartLineState, "checking">;
  messages: string[];
  refreshedLine?: CartLine;
}
