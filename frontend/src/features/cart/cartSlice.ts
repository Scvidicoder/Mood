import { createSelector, createSlice, type PayloadAction } from "@reduxjs/toolkit";
import {
  MAX_CART_LINES,
  MAX_CART_QUANTITY,
  type CartLine,
  type CartLineRevalidation,
  type CartState,
} from "../../types/cart";

export const initialCartState: CartState = {
  items: [],
};

const cartSlice = createSlice({
  name: "cart",
  initialState: initialCartState,
  reducers: {
    addConfiguredLine(state, action: PayloadAction<CartLine>) {
      const incoming = action.payload;
      const existing = state.items.find(
        (line) => line.configurationKey === incoming.configurationKey,
      );
      if (existing) {
        existing.quantity = Math.min(
          MAX_CART_QUANTITY,
          existing.quantity + incoming.quantity,
        );
        existing.updatedAt = incoming.updatedAt;
        existing.state = "valid";
        existing.messages = [];
        state.announcement = `${existing.productName} quantity is now ${existing.quantity}.`;
        return;
      }
      if (state.items.length >= MAX_CART_LINES) {
        state.announcement =
          "The cart has reached its device limit. Remove an item before adding another configuration.";
        return;
      }
      state.items.push(incoming);
      state.announcement = `${incoming.productName} was added to your cart.`;
    },
    replaceConfiguredLine(
      state,
      action: PayloadAction<{ lineId: string; line: CartLine }>,
    ) {
      const currentIndex = state.items.findIndex(
        (line) => line.id === action.payload.lineId,
      );
      if (currentIndex < 0) {
        return;
      }

      const current = state.items[currentIndex];
      const duplicateIndex = state.items.findIndex(
        (line, index) =>
          index !== currentIndex &&
          line.configurationKey === action.payload.line.configurationKey,
      );
      if (duplicateIndex >= 0) {
        const duplicate = state.items[duplicateIndex];
        duplicate.quantity = Math.min(
          MAX_CART_QUANTITY,
          duplicate.quantity + current.quantity,
        );
        duplicate.updatedAt = action.payload.line.updatedAt;
        duplicate.state = "valid";
        duplicate.messages = [];
        state.items.splice(currentIndex, 1);
        state.announcement = `${duplicate.productName} configurations were merged.`;
        return;
      }

      state.items[currentIndex] = {
        ...action.payload.line,
        id: current.id,
        quantity: current.quantity,
        createdAt: current.createdAt,
      };
      state.announcement = `${action.payload.line.productName} was updated.`;
    },
    increaseQuantity(state, action: PayloadAction<string>) {
      const line = state.items.find((item) => item.id === action.payload);
      if (!line) {
        return;
      }
      if (line.quantity >= MAX_CART_QUANTITY) {
        state.announcement = `The device limit is ${MAX_CART_QUANTITY} of one configuration.`;
        return;
      }
      line.quantity += 1;
      line.updatedAt = new Date().toISOString();
      state.announcement = `${line.productName} quantity is now ${line.quantity}.`;
    },
    decreaseQuantity(state, action: PayloadAction<string>) {
      const index = state.items.findIndex((item) => item.id === action.payload);
      if (index < 0) {
        return;
      }
      const line = state.items[index];
      if (line.quantity === 1) {
        state.items.splice(index, 1);
        state.announcement = `${line.productName} was removed from your cart.`;
        return;
      }
      line.quantity -= 1;
      line.updatedAt = new Date().toISOString();
      state.announcement = `${line.productName} quantity is now ${line.quantity}.`;
    },
    removeLine(state, action: PayloadAction<string>) {
      const index = state.items.findIndex((item) => item.id === action.payload);
      if (index < 0) {
        return;
      }
      const [removed] = state.items.splice(index, 1);
      state.announcement = `${removed.productName} was removed from your cart.`;
    },
    clearCart(state) {
      if (state.items.length === 0) {
        return;
      }
      state.items = [];
      state.announcement = "Your cart was cleared.";
    },
    applyRevalidation(
      state,
      action: PayloadAction<CartLineRevalidation[]>,
    ) {
      for (const outcome of action.payload) {
        const index = state.items.findIndex(
          (line) => line.id === outcome.lineId,
        );
        if (index < 0) {
          continue;
        }
        const current = state.items[index];
        state.items[index] = outcome.refreshedLine
          ? {
              ...outcome.refreshedLine,
              id: current.id,
              quantity: current.quantity,
              createdAt: current.createdAt,
              state: outcome.state,
              messages: outcome.messages,
            }
          : {
              ...current,
              state: outcome.state,
              messages: outcome.messages,
            };
      }
    },
    persistenceFailed(state) {
      state.persistenceWarning =
        "Your cart still works, but browser storage could not be updated. It may not survive a refresh.";
    },
    dismissCartNotice(state) {
      state.restorationNotice = undefined;
      state.persistenceWarning = undefined;
    },
    clearAnnouncement(state) {
      state.announcement = undefined;
    },
  },
});

export const cartActions = cartSlice.actions;
export const cartReducer = cartSlice.reducer;

type CartRootState = { cart: CartState };

export const selectCart = (state: CartRootState) => state.cart;
export const selectCartItems = (state: CartRootState) => state.cart.items;
export const selectCartLine = (lineId: string | undefined) =>
  createSelector(selectCartItems, (items) =>
    lineId ? items.find((line) => line.id === lineId) : undefined,
  );
export const selectCartTotalQuantity = createSelector(
  selectCartItems,
  (items) => items.reduce((total, line) => total + line.quantity, 0),
);
export const selectCartSubtotalMinor = createSelector(
  selectCartItems,
  (items) =>
    items.reduce(
      (total, line) => total + line.unitPriceMinor * line.quantity,
      0,
    ),
);
export const selectCartHasBlockingIssues = createSelector(
  selectCartItems,
  (items) =>
    items.some(
      (line) =>
        line.state === "checking" ||
        line.state === "needsAttention" ||
        line.state === "unavailable",
    ),
);
