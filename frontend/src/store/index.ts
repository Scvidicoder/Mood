import {
  configureStore,
  isAnyOf,
  type Middleware,
} from "@reduxjs/toolkit";
import { useDispatch, useSelector } from "react-redux";
import {
  cartActions,
  cartReducer,
  initialCartState,
} from "../features/cart/cartSlice";
import {
  browserCartStorage,
  persistCart,
  restoreCart,
  type CartStorage,
} from "../features/cart/cartStorage";

const persistedCartActions = isAnyOf(
  cartActions.addConfiguredLine,
  cartActions.replaceConfiguredLine,
  cartActions.increaseQuantity,
  cartActions.decreaseQuantity,
  cartActions.removeLine,
  cartActions.clearCart,
  cartActions.applyRevalidation,
);

export function createAppStore(
  storage: CartStorage | undefined = browserCartStorage(),
) {
  const restored = restoreCart(storage);
  const persistenceMiddleware: Middleware = (api) => (next) => (action) => {
    const previousCart = (api.getState() as { cart: typeof initialCartState }).cart;
    const result = next(action);
    const nextCart = (api.getState() as { cart: typeof initialCartState }).cart;

    if (
      persistedCartActions(action) &&
      previousCart.items !== nextCart.items &&
      storage
    ) {
      try {
        persistCart(storage, nextCart.items);
      } catch {
        api.dispatch(cartActions.persistenceFailed());
      }
    }
    return result;
  };

  return configureStore({
    reducer: {
      cart: cartReducer,
    },
    preloadedState: {
      cart: {
        ...initialCartState,
        items: restored.items,
        restorationNotice: restored.notice,
      },
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(persistenceMiddleware),
  });
}

export const store = createAppStore();

export type AppStore = ReturnType<typeof createAppStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];

export const useAppDispatch = useDispatch.withTypes<AppDispatch>();
export const useAppSelector = useSelector.withTypes<RootState>();
