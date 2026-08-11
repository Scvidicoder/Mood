import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider as ReduxProvider } from "react-redux";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import { CheckoutPage } from "../pages/CheckoutPage";
import { createAppStore, type AppStore } from "../store";
import { cartActions } from "../features/cart/cartSlice";
import { CART_STORAGE_KEY } from "../types/cart";
import { cartLine } from "./cartTestFixtures";

const mocks = vi.hoisted(() => ({
  createOrder: vi.fn(),
}));

vi.mock("../api/orders", () => ({
  createOrder: mocks.createOrder,
}));

vi.mock("../features/cart/useCartRevalidation", () => ({
  useCartRevalidation: () => ({
    isFetching: false,
    refresh: vi.fn(),
  }),
}));

describe("customer checkout", () => {
  beforeEach(() => {
    mocks.createOrder.mockReset();
    window.localStorage.clear();
  });

  it("submits only cart identifiers, clears cart storage, and navigates after creation", async () => {
    const user = userEvent.setup();
    const store = createStoreWithLine();
    window.localStorage.setItem(CART_STORAGE_KEY, "persisted-cart-preview");
    mocks.createOrder.mockResolvedValue(createdOrder());
    renderCheckout(store);

    await user.type(
      screen.getByRole("textbox", { name: /Comment for the café/i }),
      "No sugar, please",
    );
    await user.click(screen.getByRole("button", { name: "Create order" }));

    expect(await screen.findByRole("heading", { name: "Order success route" })).toBeVisible();
    expect(mocks.createOrder.mock.calls[0][0]).toEqual({
      items: [
        {
          productId: cartLine().productId,
          optionValueIds: cartLine().selectedOptions.map((option) => option.optionValueId),
          quantity: 1,
          comment: null,
        },
      ],
      comment: "No sugar, please",
      paymentMethod: "PayOnPickup",
      pickupMode: "AsSoonAsPossible",
      requestedPickupTime: null,
    });
    expect(store.getState().cart.items).toEqual([]);
    expect(window.localStorage.getItem(CART_STORAGE_KEY)).toBeNull();
  });

  it("keeps the cart intact and shows server validation failures for retry", async () => {
    const user = userEvent.setup();
    const store = createStoreWithLine();
    mocks.createOrder.mockRejectedValue(
      new ApiError(
        "One selected option is no longer available.",
        400,
        "VALIDATION_ERROR",
        undefined,
        { "items[0]": ["One selected option is no longer available."] },
      ),
    );
    renderCheckout(store);

    await user.click(screen.getByRole("button", { name: "Create order" }));

    expect(await screen.findByText("One selected option is no longer available.")).toBeVisible();
    expect(store.getState().cart.items).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Create order" })).toBeEnabled();
  });

  it("requires a requested time when scheduled pickup is selected", async () => {
    const user = userEvent.setup();
    mocks.createOrder.mockResolvedValue(createdOrder());
    renderCheckout(createStoreWithLine());

    await user.click(screen.getByRole("radio", { name: /Schedule a pickup/i }));
    await user.click(screen.getByRole("button", { name: "Create order" }));

    expect(await screen.findByText("Choose a requested pickup time.")).toBeVisible();
    expect(mocks.createOrder).not.toHaveBeenCalled();
  });
});

function renderCheckout(store: AppStore) {
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false }, queries: { retry: false } },
  });
  return render(
    <ReduxProvider store={store}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={["/checkout"]}>
          <Routes>
            <Route element={<CheckoutPage />} path="/checkout" />
            <Route path="/order-success/:id" element={<h1>Order success route</h1>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ReduxProvider>,
  );
}

function createStoreWithLine(): AppStore {
  const store = createAppStore({
    getItem: () => null,
    setItem: () => undefined,
  });
  store.dispatch(cartActions.addConfiguredLine(cartLine()));
  return store;
}

function createdOrder() {
  return {
    id: "order-1",
    orderNumber: "MP-20260807-00001",
    status: "PendingConfirmation" as const,
    paymentMethod: "PayOnPickup" as const,
    pickupMode: "AsSoonAsPossible" as const,
    subtotal: 24,
    discountTotal: 0,
    total: 24,
    currency: "TJS",
    createdAt: "2026-08-07T10:00:00.000Z",
    items: [],
  };
}
