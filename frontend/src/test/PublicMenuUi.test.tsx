import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";
import { Provider as ReduxProvider } from "react-redux";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HomePage } from "../pages/HomePage";
import { ProductDetailsPage } from "../pages/ProductDetailsPage";
import { ToastProvider } from "../components/ToastProvider";
import { createAppStore } from "../store";

const coffeeId = "11111111-1111-1111-1111-111111111111";
const dessertId = "22222222-2222-2222-2222-222222222222";
const cappuccinoId = "33333333-3333-3333-3333-333333333333";
const cheesecakeId = "44444444-4444-4444-4444-444444444444";

const categories = [
  {
    id: coffeeId,
    name: "Coffee",
    description: "Espresso and milk drinks",
    displayOrder: 0,
  },
  {
    id: dessertId,
    name: "Desserts",
    description: "Something sweet",
    displayOrder: 1,
  },
];

const cappuccino = {
  id: cappuccinoId,
  categoryId: coffeeId,
  name: "Cappuccino",
  shortDescription: "Espresso with steamed milk",
  imageUrl: "/media/aa/bb/cappuccino.webp",
  priceFrom: 24,
  currency: "TJS",
  weightGrams: null,
  volumeMilliliters: 250,
  calories: 120,
  isAvailable: true,
  isOrderable: true,
  availabilityIssues: [],
};

const cheesecake = {
  id: cheesecakeId,
  categoryId: dessertId,
  name: "Cheesecake",
  shortDescription: "Creamy seasonal dessert",
  imageUrl: null,
  priceFrom: 35,
  currency: "TJS",
  weightGrams: 140,
  volumeMilliliters: null,
  calories: 420,
  isAvailable: false,
  isOrderable: false,
  availabilityIssues: [
    {
      code: "PRODUCT_UNAVAILABLE",
      message: "The product is unavailable.",
      productOptionGroupId: null,
    },
  ],
};

const cappuccinoDetail = {
  id: cappuccinoId,
  categoryId: coffeeId,
  name: "Cappuccino",
  description: "A balanced espresso with silky steamed milk.",
  ingredients: "Espresso, milk",
  imageUrl: "/media/aa/bb/cappuccino.webp",
  basePrice: 22,
  priceFrom: 24,
  currency: "TJS",
  weightGrams: null,
  volumeMilliliters: 250,
  calories: 120,
  isAvailable: true,
  isOrderable: true,
  availabilityIssues: [],
  optionGroups: [
    {
      id: "55555555-5555-5555-5555-555555555555",
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
          optionValueId: "77777777-7777-7777-7777-777777777777",
          name: "Small",
          description: null,
          priceModifier: 2,
          isDefault: true,
          isAvailable: true,
          displayOrder: 0,
          volumeMilliliters: 200,
          calories: 100,
        },
        {
          id: "88888888-8888-8888-8888-888888888888",
          optionValueId: "99999999-9999-9999-9999-999999999999",
          name: "Large",
          description: null,
          priceModifier: 8,
          isDefault: false,
          isAvailable: false,
          displayOrder: 1,
          volumeMilliliters: 450,
          calories: 190,
        },
      ],
    },
  ],
};

describe("public customer menu", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders categories, grouped cards, metrics, and structured orderability", async () => {
    mockMenuFetch();
    renderMenu();

    expect(await screen.findByRole("heading", { name: "Coffee" })).toBeVisible();
    expect(screen.getByRole("heading", { name: "Desserts" })).toBeVisible();
    const cappuccinoCard = screen
      .getByText("Cappuccino")
      .closest("article");
    expect(cappuccinoCard).not.toBeNull();
    expect(within(cappuccinoCard!).getByText("120 kcal")).toBeVisible();
    expect(within(cappuccinoCard!).getByText("250 ml")).toBeVisible();
    const cheesecakeCard = screen.getByText("Cheesecake").closest("article");
    expect(within(cheesecakeCard!).getByText("Unavailable")).toBeVisible();
    expect(
      within(cheesecakeCard!).getByText("The product is unavailable."),
    ).toBeVisible();
    expect(
      screen.getByRole("navigation", { name: "Menu categories" }),
    ).toBeVisible();
    expect(screen.getByRole("search")).toBeVisible();
    expect(screen.getByLabelText("Search the menu")).toHaveAttribute(
      "type",
      "search",
    );
  });

  it("debounces server search, synchronizes the URL, and clears it", async () => {
    const user = userEvent.setup();
    const fetchMock = mockMenuFetch((url) =>
      url.searchParams.get("search") === "seasonal" ? [cheesecake] : [cappuccino, cheesecake],
    );
    renderMenu();
    await screen.findByText("Cappuccino");

    await user.type(screen.getByLabelText("Search the menu"), "seasonal");
    await waitFor(() =>
      expect(screen.getByTestId("location-search")).toHaveTextContent(
        "search=seasonal",
      ),
    );
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) =>
          String(input).includes("search=seasonal"),
        ),
      ).toBe(true),
    );
    expect(await screen.findByText("Cheesecake")).toBeVisible();
    expect(screen.queryByText("Cappuccino")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Clear menu search" }));
    await waitFor(() =>
      expect(screen.getByTestId("location-search")).toBeEmptyDOMElement(),
    );
    expect(await screen.findByText("Cappuccino")).toBeVisible();
  });

  it("filters by category and responds to browser back navigation", async () => {
    const user = userEvent.setup();
    const fetchMock = mockMenuFetch((url) => {
      const categoryId = url.searchParams.get("categoryId");
      return categoryId === dessertId ? [cheesecake] : [cappuccino, cheesecake];
    });
    renderMenu();
    await screen.findByText("Cappuccino");

    await user.click(
      screen.getByRole("link", { name: "Desserts", current: false }),
    );
    await waitFor(() =>
      expect(screen.getByTestId("location-search")).toHaveTextContent(
        `category=${dessertId}`,
      ),
    );
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) =>
          String(input).includes(`categoryId=${dessertId}`),
        ),
      ).toBe(true),
    );
    expect(await screen.findByRole("heading", { name: "Desserts" })).toBeVisible();
    expect(screen.queryByText("Cappuccino")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Browser back" }));
    await waitFor(() =>
      expect(screen.getByTestId("location-search")).toBeEmptyDOMElement(),
    );
    expect(await screen.findByText("Cappuccino")).toBeVisible();
  });

  it("navigates to accessible interactive product details with valid defaults", async () => {
    const user = userEvent.setup();
    mockMenuFetch();
    renderMenu();
    await screen.findByText("Cappuccino");

    await user.click(
      screen.getByRole("link", { name: "View details for Cappuccino" }),
    );

    expect(
      await screen.findByRole("heading", { level: 1, name: "Cappuccino" }),
    ).toBeVisible();
    expect(screen.getByText("Espresso, milk")).toBeVisible();
    expect(screen.getByRole("group", { name: "Size" })).toBeVisible();
    expect(screen.getByText("Default")).toBeVisible();
    expect(screen.getByText("+TJS 2.00")).toBeVisible();
    expect(screen.getByRole("radio", { name: /Small/ })).toBeChecked();
    const large = screen.getByText("Large").closest("label");
    expect(within(large!).getByText("Unavailable")).toBeVisible();
    expect(within(large!).getByText("190 kcal")).toBeVisible();
    expect(screen.getByRole("button", { name: "Add to Cart" })).toBeEnabled();
    expect(screen.getAllByText("TJS 24.00")).toHaveLength(2);
    expect(
      screen.getByRole("heading", { level: 1, name: "Cappuccino" }),
    ).toHaveFocus();
  });

  it("opens the configurator directly from the card Add action", async () => {
    const user = userEvent.setup();
    mockMenuFetch();
    renderMenu();

    await user.click(await screen.findByRole("button", { name: "Add" }));

    expect(screen.getByRole("dialog")).toBeVisible();
    expect(
      screen.getByRole("heading", { name: "Cappuccino", level: 2 }),
    ).toBeVisible();
    expect(
      screen.queryByRole("heading", { name: "Cappuccino", level: 1 }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Add to Cart" }));
    expect(await screen.findByText("Cappuccino added to your cart.")).toBeVisible();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("quick-adds an item with no required configuration", async () => {
    const user = userEvent.setup();
    const simpleDetail = { ...cappuccinoDetail, optionGroups: [] };
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith("/categories")) return json(categories);
      if (url.pathname.endsWith(`/products/${cappuccinoId}`)) return json(simpleDetail);
      if (url.pathname.endsWith("/products")) return page([cappuccino]);
      throw new Error(`Unexpected request: ${url}`);
    });
    renderMenu();

    await user.click(await screen.findByRole("button", { name: "Add" }));

    expect(await screen.findByRole("button", { name: "✓ Added" })).toBeVisible();
    expect(screen.getByText("Cappuccino added to your cart.")).toBeVisible();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("replaces a broken product image with an accessible placeholder", async () => {
    mockMenuFetch();
    renderMenu();
    const image = await screen.findByRole("img", { name: "Cappuccino" });

    fireEvent.error(image);

    expect(
      screen.getByRole("img", { name: "No image available for Cappuccino" }),
    ).toBeVisible();
    expect(screen.queryByRole("img", { name: "Cappuccino" })).not.toBeInTheDocument();
  });

  it("shows stable loading skeletons while the menu request is slow", async () => {
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      if (String(input).endsWith("/categories")) {
        return json(categories);
      }
      return new Promise<Response>(() => undefined);
    });
    renderMenu();

    expect(screen.getByRole("status", { name: "Loading menu" })).toBeVisible();
  });

  it("offers retry after a public-menu error", async () => {
    const user = userEvent.setup();
    let productRequests = 0;
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      const url = String(input);
      if (url.endsWith("/categories")) {
        return json(categories);
      }
      if (url.includes("/products/")) {
        return json(cappuccinoDetail);
      }
      productRequests += 1;
      return productRequests === 1
        ? json({ title: "Menu unavailable", detail: "Please try again." }, 503)
        : page([cappuccino, cheesecake]);
    });
    renderMenu();

    expect(await screen.findByText("Please try again.")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Retry menu" }));

    expect(await screen.findByText("Cappuccino")).toBeVisible();
    expect(productRequests).toBe(2);
  });
});

function renderMenu(initialEntry = "/") {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
  return render(
    <ReduxProvider store={createAppStore(emptyStorage())}>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
              <Route
                path="/"
                element={
                  <>
                    <HomePage />
                    <LocationProbe />
                    <HistoryBack />
                  </>
                }
              />
              <Route path="/product/:id" element={<ProductDetailsPage />} />
            </Routes>
          </MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>
    </ReduxProvider>,
  );
}

function emptyStorage() {
  return {
    getItem: () => null,
    setItem: () => undefined,
  };
}

function LocationProbe() {
  const location = useLocation();
  return (
    <output data-testid="location-search" hidden>
      {location.search.replace(/^\?/, "")}
    </output>
  );
}

function HistoryBack() {
  const navigate = useNavigate();
  return (
    <button
      className="visually-hidden"
      onClick={() => navigate(-1)}
      type="button"
    >
      Browser back
    </button>
  );
}

function mockMenuFetch(
  productsForUrl: (url: URL) => unknown[] = () => [cappuccino, cheesecake],
) {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
    const url = new URL(String(input));
    if (url.pathname.endsWith("/categories")) {
      return json(categories);
    }
    if (url.pathname.endsWith(`/products/${cappuccinoId}`)) {
      return json(cappuccinoDetail);
    }
    if (url.pathname.endsWith("/products")) {
      return page(productsForUrl(url));
    }
    throw new Error(`Unexpected request: ${url}`);
  });
}

function page(items: unknown[]) {
  return json({
    items,
    page: 1,
    pageSize: 100,
    totalCount: items.length,
    totalPages: 1,
  });
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type":
        status >= 400 ? "application/problem+json" : "application/json",
    },
  });
}
