import {
  buildCartLine,
  createInitialSelection,
  validateConfiguration,
  type ProductSelection,
} from "../features/cart/configuration";
import type { CartLine } from "../types/cart";
import type { PublicProductDetail } from "../types/menu";

export const productId = "33333333-3333-3333-3333-333333333333";
export const sizeGroupId = "55555555-5555-5555-5555-555555555555";
export const smallId = "77777777-7777-7777-7777-777777777777";
export const largeId = "99999999-9999-9999-9999-999999999999";
export const syrupGroupId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
export const vanillaId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
export const caramelId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
export const hazelnutId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

export function configurableProduct(
  overrides: Partial<PublicProductDetail> = {},
): PublicProductDetail {
  return {
    id: productId,
    categoryId: "11111111-1111-1111-1111-111111111111",
    name: "Cappuccino",
    description: "Espresso with steamed milk",
    ingredients: "Espresso, milk",
    imageUrl: "/media/aa/bb/cappuccino.webp",
    basePrice: 22,
    priceFrom: 24,
    currency: "TJS",
    volumeMilliliters: 250,
    calories: 120,
    isAvailable: true,
    isOrderable: true,
    availabilityIssues: [],
    optionGroups: [
      {
        id: sizeGroupId,
        name: "Size",
        description: "Choose your cup",
        selectionType: "Single",
        isRequired: true,
        minimumSelections: 1,
        maximumSelections: 1,
        displayOrder: 0,
        values: [
          {
            id: "66666666-6666-6666-6666-666666666666",
            optionValueId: smallId,
            name: "Small",
            priceModifier: 2,
            isDefault: true,
            isAvailable: true,
            displayOrder: 0,
            volumeMilliliters: 200,
            calories: 100,
          },
          {
            id: "88888888-8888-8888-8888-888888888888",
            optionValueId: largeId,
            name: "Large",
            priceModifier: 8,
            isDefault: false,
            isAvailable: true,
            displayOrder: 1,
            volumeMilliliters: 450,
            calories: 190,
          },
        ],
      },
      {
        id: syrupGroupId,
        name: "Syrups with an intentionally long customer-facing label",
        selectionType: "Multiple",
        isRequired: false,
        minimumSelections: 0,
        maximumSelections: 2,
        displayOrder: 1,
        values: [
          {
            id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            optionValueId: vanillaId,
            name: "Vanilla",
            priceModifier: 3,
            isDefault: false,
            isAvailable: true,
            displayOrder: 0,
          },
          {
            id: "ffffffff-ffff-ffff-ffff-ffffffffffff",
            optionValueId: caramelId,
            name: "Caramel",
            priceModifier: 3,
            isDefault: false,
            isAvailable: true,
            displayOrder: 1,
          },
          {
            id: "12121212-1212-1212-1212-121212121212",
            optionValueId: hazelnutId,
            name: "Hazelnut",
            priceModifier: 3,
            isDefault: false,
            isAvailable: false,
            displayOrder: 2,
          },
        ],
      },
    ],
    ...overrides,
  };
}

export function defaultSelection(
  product = configurableProduct(),
): ProductSelection {
  return createInitialSelection(product).selection;
}

export function cartLine(
  options: {
    id?: string;
    product?: PublicProductDetail;
    quantity?: number;
    selection?: ProductSelection;
  } = {},
): CartLine {
  const product = options.product ?? configurableProduct();
  const selection = options.selection ?? defaultSelection(product);
  const result = validateConfiguration(product, selection);
  return buildCartLine(product, selection, result, {
    id: options.id ?? "line-1",
    quantity: options.quantity,
    createdAt: "2026-08-06T10:00:00.000Z",
    now: "2026-08-06T10:00:00.000Z",
  });
}
