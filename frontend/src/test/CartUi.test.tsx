import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider as ReduxProvider } from "react-redux";
import {
  MemoryRouter,
  Outlet,
  Route,
  Routes,
} from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../app/AuthProvider";
import { ProductConfigurator } from "../components/ProductConfigurator";
import { cartActions } from "../features/cart/cartSlice";
import { ApplicationLayout } from "../layouts/ApplicationLayout";
import { CartPage } from "../pages/CartPage";
import { createAppStore, type AppStore } from "../store";
import {
  caramelId,
  cartLine,
  configurableProduct,
  largeId,
  sizeGroupId,
  syrupGroupId,
  vanillaId,
} from "./cartTestFixtures";

describe("interactive product configurator", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("uses radio and checkbox semantics, replaces a single choice, enforces max, and updates price", async () => {
    const user = userEvent.setup();
    const product = configurableProduct();
    product.optionGroups[1].values[2].isAvailable = true;
    const submit = vi.fn();
    render(
      <ProductConfigurator
        onSubmit={submit}
        product={product}
        submitLabel="Add to cart"
      />,
    );

    const small = screen.getByRole("radio", { name: /Small/ });
    const large = screen.getByRole("radio", { name: /Large/ });
    expect(small).toBeChecked();
    await user.click(large);
    expect(small).not.toBeChecked();
    expect(large).toBeChecked();
    expect(screen.getByText("TJS 30.00")).toBeVisible();

    await user.click(screen.getByRole("checkbox", { name: /Vanilla/ }));
    await user.click(screen.getByRole("checkbox", { name: /Caramel/ }));
    expect(screen.getByRole("checkbox", { name: /Hazelnut/ })).toBeDisabled();
    expect(screen.getByText(/Maximum 2 selected/)).toBeVisible();
    expect(screen.getByText("TJS 36.00")).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Add to cart" }));
    expect(submit).toHaveBeenCalledOnce();
    expect(submit.mock.calls[0][0]).toMatchObject({
      [sizeGroupId]: [largeId],
      [syrupGroupId]: [vanillaId, caramelId],
    });
  });

  it("shows structured validation and disables Add to cart until a required choice is made", async () => {
    const user = userEvent.setup();
    const product = configurableProduct();
    product.optionGroups[0].values = product.optionGroups[0].values.map(
      (value) => ({ ...value, isDefault: false }),
    );
    render(
      <ProductConfigurator
        onSubmit={vi.fn()}
        product={product}
        submitLabel="Add to cart"
      />,
    );

    expect(screen.getByText("Choose one option for Size.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Add to cart" })).toBeDisabled();
    await user.click(screen.getByRole("radio", { name: /Large/ }));
    expect(screen.getByRole("button", { name: "Add to cart" })).toBeEnabled();
  });

  it("supports keyboard selection and prefilled cart editing", async () => {
    const user = userEvent.setup();
    const submit = vi.fn();
    render(
      <ProductConfigurator
        initialOptionValueIds={[largeId, vanillaId]}
        onSubmit={submit}
        product={configurableProduct()}
        submitLabel="Save cart changes"
      />,
    );

    expect(screen.getByRole("radio", { name: /Large/ })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: /Vanilla/ })).toBeChecked();
    screen.getByRole("checkbox", { name: /Caramel/ }).focus();
    await user.keyboard("[Space]");
    expect(screen.getByRole("checkbox", { name: /Caramel/ })).toBeChecked();
    await user.click(
      screen.getByRole("button", { name: "Save cart changes" }),
    );
    expect(submit).toHaveBeenCalledOnce();
  });

  it("keeps unavailable values visible and unselectable", () => {
    render(
      <ProductConfigurator
        onSubmit={vi.fn()}
        product={configurableProduct()}
        submitLabel="Add to cart"
      />,
    );
    const hazelnut = screen.getByRole("checkbox", { name: /Hazelnut/ });
    expect(hazelnut).toBeDisabled();
    expect(
      within(hazelnut.closest("label")!).getByText("Unavailable"),
    ).toBeVisible();
  });
});

describe("local cart UI", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("shows an empty state and a link back to the menu", () => {
    renderCart(createAppStore(emptyStorage()));
    expect(
      screen.getByRole("heading", { name: "Your cart is empty." }),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Browse the menu" })).toHaveAttribute(
      "href",
      "/",
    );
  });

  it("renders cart totals, accessible quantity actions, remove, and clear", async () => {
    const user = userEvent.setup();
    mockProduct();
    const store = createAppStore(emptyStorage());
    store.dispatch(cartActions.addConfiguredLine(cartLine({ quantity: 2 })));
    renderCart(store);

    expect(await screen.findAllByText("TJS 48.00")).toHaveLength(2);
    expect(screen.getByText("2 items stored on this device.")).toBeVisible();
    await user.click(
      screen.getByRole("button", {
        name: "Increase Cappuccino quantity",
      }),
    );
    expect(screen.getByText("3 items stored on this device.")).toBeVisible();
    expect(screen.getAllByText("TJS 72.00")).toHaveLength(2);

    await user.click(
      screen.getByRole("button", {
        name: "Decrease Cappuccino quantity",
      }),
    );
    expect(screen.getByText("2 items stored on this device.")).toBeVisible();
    await user.click(
      screen.getByRole("button", {
        name: "Remove Cappuccino from cart",
      }),
    );
    expect(
      screen.getByRole("heading", { name: "Your cart is empty." }),
    ).toBeVisible();

    act(() => {
      store.dispatch(cartActions.addConfiguredLine(cartLine()));
    });
    expect(
      await screen.findByRole("button", { name: "Clear cart" }),
    ).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Clear cart" }));
    expect(
      screen.getByRole("heading", { name: "Your cart is empty." }),
    ).toBeVisible();
  });

  it("shows the header badge immediately and after restored state", async () => {
    const store = createAppStore(emptyStorage());
    store.dispatch(cartActions.addConfiguredLine(cartLine({ quantity: 3 })));
    render(
      <ReduxProvider store={store}>
        <MemoryRouter>
          <AuthProvider>
            <Routes>
              <Route element={<ApplicationLayout />} path="/">
                <Route index element={<Outlet />} />
              </Route>
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </ReduxProvider>,
    );

    expect(
      await screen.findByRole("link", { name: "Cart with 3 items" }),
    ).toBeVisible();
    expect(
      within(screen.getByRole("navigation", { name: "Primary navigation" }))
        .getByText("3"),
    ).toBeVisible();
  });

  it("links to the shared configurator with the cart line ID", async () => {
    mockProduct();
    const store = createAppStore(emptyStorage());
    store.dispatch(cartActions.addConfiguredLine(cartLine()));
    renderCart(store);
    const edit = await screen.findByRole("link", { name: "Edit options" });
    expect(edit).toHaveAttribute(
      "href",
      expect.stringContaining("?editLine=line-1"),
    );
  });
});

function renderCart(store: AppStore) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
  return render(
    <ReduxProvider store={store}>
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={["/cart"]}>
          <Routes>
            <Route element={<CartPage />} path="/cart" />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ReduxProvider>,
  );
}

function mockProduct() {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async () =>
    json(configurableProduct()),
  );
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}

function emptyStorage() {
  return {
    getItem: () => null,
    setItem: () => undefined,
  };
}
