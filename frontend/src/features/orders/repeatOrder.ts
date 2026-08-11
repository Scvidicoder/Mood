import {
  CART_CURRENCY,
  type CartLine,
} from "../../types/cart";
import type { RepeatOrderItem } from "../../types/orders";
import {
  createConfigurationKey,
  moneyToMinor,
} from "../cart/configuration";

export function repeatItemToCartLine(
  item: RepeatOrderItem,
  now = new Date().toISOString(),
): CartLine {
  const basePriceMinor = moneyToMinor(item.basePrice);
  const unitPriceMinor = moneyToMinor(item.unitPrice);
  if (
    item.currency !== CART_CURRENCY ||
    basePriceMinor == null ||
    unitPriceMinor == null
  ) {
    throw new Error("This repeated item cannot be represented safely in the cart.");
  }

  const selectedOptions = item.options.map((option) => ({
    groupId: option.productOptionGroupId,
    groupName: option.optionGroupName,
    optionValueId: option.optionValueId,
    valueName: option.optionValueName,
    priceModifierMinor: moneyToMinor(option.priceModifier) ?? 0,
    volumeMilliliters: option.volumeMilliliters,
    calories: option.calories,
  }));

  return {
    id: createLocalId(),
    configurationKey: createConfigurationKey(
      item.productId,
      selectedOptions.map((option) => option.optionValueId),
    ),
    productId: item.productId,
    productName: item.productName,
    basePriceMinor,
    currency: CART_CURRENCY,
    selectedOptions,
    quantity: item.quantity,
    unitPriceMinor,
    createdAt: now,
    updatedAt: now,
    state: "valid",
    messages: [],
  };
}

function createLocalId(): string {
  return globalThis.crypto?.randomUUID?.() ??
    `repeat-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
