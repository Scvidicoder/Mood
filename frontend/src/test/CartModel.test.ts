import { describe, expect, it } from "vitest";
import {
  cartActions,
  cartReducer,
  initialCartState,
  selectCartSubtotalMinor,
  selectCartTotalQuantity,
} from "../features/cart/cartSlice";
import {
  createConfigurationKey,
  createInitialSelection,
  validateConfiguration,
} from "../features/cart/configuration";
import {
  caramelId,
  cartLine,
  configurableProduct,
  defaultSelection,
  largeId,
  sizeGroupId,
  smallId,
  syrupGroupId,
  vanillaId,
} from "./cartTestFixtures";

describe("product configuration model", () => {
  it("applies valid available defaults and calculates minor-unit price and metrics", () => {
    const product = configurableProduct();
    const initial = createInitialSelection(product);
    const result = validateConfiguration(product, initial.selection);

    expect(initial.warnings).toEqual([]);
    expect(initial.selection[sizeGroupId]).toEqual([smallId]);
    expect(result.isValid).toBe(true);
    expect(result.unitPriceMinor).toBe(2400);
    expect(result.volumeMilliliters).toBe(200);
    expect(result.calories).toBe(100);
  });

  it("reports required single, minimum, maximum, and unavailable option issues", () => {
    const product = configurableProduct({
      optionGroups: configurableProduct().optionGroups.map((group) =>
        group.id === syrupGroupId
          ? { ...group, minimumSelections: 1 }
          : group,
      ),
    });

    const missing = validateConfiguration(product, {
      [sizeGroupId]: [],
      [syrupGroupId]: [],
    });
    expect(missing.issues.map((issue) => issue.code)).toEqual(
      expect.arrayContaining([
        "MINIMUM_SELECTIONS_NOT_MET",
      ]),
    );

    const excessive = validateConfiguration(product, {
      [sizeGroupId]: [smallId, largeId],
      [syrupGroupId]: [vanillaId, caramelId, "missing-option"],
    });
    expect(excessive.issues.map((issue) => issue.code)).toEqual(
      expect.arrayContaining([
        "SINGLE_SELECTION_LIMIT",
        "MAXIMUM_SELECTIONS_EXCEEDED",
        "OPTION_VALUE_MISSING",
      ]),
    );
  });

  it("does not select unavailable defaults and blocks contradictory defaults", () => {
    const product = configurableProduct();
    product.optionGroups[0].values[0].isAvailable = false;
    product.optionGroups[0].values[1].isDefault = true;
    const unavailableDefault = createInitialSelection(product);
    expect(unavailableDefault.selection[sizeGroupId]).toEqual([largeId]);
    expect(unavailableDefault.warnings[0].code).toBe(
      "DEFAULT_VALUE_UNAVAILABLE",
    );

    product.optionGroups[0].values[0].isAvailable = true;
    product.optionGroups[0].values[0].isDefault = true;
    const contradictory = createInitialSelection(product);
    expect(contradictory.selection[sizeGroupId]).toEqual([]);
    expect(contradictory.warnings[0].code).toBe(
      "CONTRADICTORY_SINGLE_DEFAULTS",
    );
    expect(
      validateConfiguration(
        product,
        contradictory.selection,
        contradictory.warnings,
      ).isValid,
    ).toBe(false);
  });

  it("supports optional single groups without inventing a selection", () => {
    const product = configurableProduct();
    product.optionGroups = [
      {
        ...product.optionGroups[0],
        isRequired: false,
        minimumSelections: 0,
        values: product.optionGroups[0].values.map((value) => ({
          ...value,
          isDefault: false,
        })),
      },
    ];
    const initial = createInitialSelection(product);
    expect(initial.selection[sizeGroupId]).toEqual([]);
    expect(validateConfiguration(product, initial.selection).isValid).toBe(
      true,
    );
  });

  it("calculates selected modifiers without floating-point display drift", () => {
    const product = configurableProduct({ basePrice: 0.1 });
    product.optionGroups[0].values[0].priceModifier = 0.2;
    const selection = defaultSelection(product);
    selection[syrupGroupId] = [vanillaId];
    product.optionGroups[1].values[0].priceModifier = 0.3;

    expect(
      validateConfiguration(product, selection).unitPriceMinor,
    ).toBe(60);
  });

  it("rejects configured prices that could overflow a full cart subtotal", () => {
    const product = configurableProduct({
      basePrice: Number.MAX_SAFE_INTEGER,
    });

    const result = validateConfiguration(
      product,
      defaultSelection(product),
    );

    expect(result.isValid).toBe(false);
    expect(result.issues.map((issue) => issue.code)).toContain(
      "INVALID_BASE_PRICE",
    );
  });
});

describe("cart reducer and selectors", () => {
  it("uses canonical option order and merges identical configurations", () => {
    expect(
      createConfigurationKey("product", ["z", "a", "z"]),
    ).toBe("product:a,z");

    const first = cartLine();
    const reordered = {
      ...cartLine({ id: "line-2" }),
      selectedOptions: [...first.selectedOptions].reverse(),
      configurationKey: createConfigurationKey(
        first.productId,
        [...first.selectedOptions]
          .reverse()
          .map((option) => option.optionValueId),
      ),
    };
    let state = cartReducer(
      initialCartState,
      cartActions.addConfiguredLine(first),
    );
    state = cartReducer(state, cartActions.addConfiguredLine(reordered));

    expect(state.items).toHaveLength(1);
    expect(state.items[0].quantity).toBe(2);
  });

  it("keeps different configurations separate", () => {
    const first = cartLine();
    const selection = defaultSelection();
    selection[sizeGroupId] = [largeId];
    const second = cartLine({ id: "line-2", selection });
    let state = cartReducer(
      initialCartState,
      cartActions.addConfiguredLine(first),
    );
    state = cartReducer(state, cartActions.addConfiguredLine(second));
    expect(state.items).toHaveLength(2);
  });

  it("increases, decreases, removes, and clears positive integer quantities", () => {
    let state = cartReducer(
      initialCartState,
      cartActions.addConfiguredLine(cartLine()),
    );
    state = cartReducer(state, cartActions.increaseQuantity("line-1"));
    expect(state.items[0].quantity).toBe(2);
    state = cartReducer(state, cartActions.decreaseQuantity("line-1"));
    expect(state.items[0].quantity).toBe(1);
    state = cartReducer(state, cartActions.decreaseQuantity("line-1"));
    expect(state.items).toEqual([]);

    state = cartReducer(
      state,
      cartActions.addConfiguredLine(cartLine({ id: "line-2" })),
    );
    state = cartReducer(state, cartActions.removeLine("line-2"));
    expect(state.items).toEqual([]);
    state = cartReducer(
      state,
      cartActions.addConfiguredLine(cartLine({ id: "line-3" })),
    );
    expect(cartReducer(state, cartActions.clearCart()).items).toEqual([]);
  });

  it("calculates line totals, subtotal, and total item quantity", () => {
    const state = {
      cart: {
        ...initialCartState,
        items: [
          cartLine({ id: "one", quantity: 2 }),
          cartLine({
            id: "two",
            quantity: 3,
            selection: {
              [sizeGroupId]: [largeId],
              [syrupGroupId]: [],
            },
          }),
        ],
      },
    };
    expect(selectCartSubtotalMinor(state)).toBe(2 * 2400 + 3 * 3000);
    expect(selectCartTotalQuantity(state)).toBe(5);
  });

  it("merges quantities when editing creates an identical configuration", () => {
    const selection = defaultSelection();
    selection[sizeGroupId] = [largeId];
    let state = {
      ...initialCartState,
      items: [
        cartLine({ id: "small", quantity: 2 }),
        cartLine({ id: "large", quantity: 3, selection }),
      ],
    };
    state = cartReducer(
      state,
      cartActions.replaceConfiguredLine({
        lineId: "large",
        line: cartLine({ id: "edited" }),
      }),
    );
    expect(state.items).toHaveLength(1);
    expect(state.items[0].quantity).toBe(5);
  });
});
