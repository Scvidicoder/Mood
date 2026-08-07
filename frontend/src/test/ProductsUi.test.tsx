import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { ProductFormPage } from "../pages/staff/menu/ProductFormPage";
import { ProductsPage } from "../pages/staff/menu/ProductsPage";

const category = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "Coffee",
  description: null,
  displayOrder: 0,
  isVisible: true,
  isDeleted: false,
  productCount: 2,
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: "2026-08-02T10:00:00Z",
  rowVersion: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
};

const listProduct = {
  id: "22222222-2222-2222-2222-222222222222",
  categoryId: category.id,
  categoryName: category.name,
  name: "Cappuccino",
  imageUrl: null,
  basePrice: 22.5,
  isAvailable: true,
  isVisible: true,
  isDeleted: false,
  isOrderable: false,
  availabilityIssues: [
    { code: "REQUIRED_GROUP_NO_DEFAULT", message: "Required group Size has no default." },
  ],
  displayOrder: 0,
  updatedAt: "2026-08-02T10:00:00Z",
  rowVersion: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
};

const secondProduct = {
  ...listProduct,
  id: "33333333-3333-3333-3333-333333333333",
  name: "Latte",
  displayOrder: 1,
  isOrderable: true,
  availabilityIssues: [],
  rowVersion: "cccccccc-cccc-cccc-cccc-cccccccccccc",
};

const product = {
  ...listProduct,
  shortDescription: "Foamy coffee",
  description: "Espresso and milk",
  ingredients: "Coffee, milk",
  defaultWeightGrams: null,
  defaultVolumeMilliliters: 300,
  defaultCalories: 120,
  imageId: null,
  image: null,
  createdAt: "2026-08-01T10:00:00Z",
  orderability: {
    isOrderable: false,
    issues: listProduct.availabilityIssues,
  },
  optionGroups: [],
};

describe("product administration", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: vi.fn(() => "blob:test-preview"),
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: vi.fn(),
    });
  });

  it("applies filters, toggles status, reorders within a category, and duplicates", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET" && url.includes("admin/categories")) {
          return json(pageOf([category]));
        }
        if (method === "GET" && url.includes("admin/products")) {
          return json(pageOf([listProduct, secondProduct]));
        }
        if (method === "PATCH") {
          return json({ resource: product, orderability: product.orderability });
        }
        if (method === "PUT" && url.endsWith("admin/products/reorder")) {
          return json([secondProduct, listProduct]);
        }
        if (method === "POST" && url.includes("/duplicate")) {
          return json({
            resource: { ...product, id: "44444444-4444-4444-4444-444444444444", name: "Copy" },
            orderability: product.orderability,
          });
        }
        throw new Error(`Unexpected request: ${method} ${url}`);
      },
    );
    renderPage(<ProductsPage />, "/staff/menu/products");

    expect(await screen.findByText("Cappuccino")).toBeVisible();
    expect(screen.getByText("Required group Size has no default.")).toBeVisible();
    await user.type(screen.getByLabelText("Search"), "Latte");
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => String(input).includes("search=Latte")),
      ).toBe(true),
    );
    await user.selectOptions(screen.getByLabelText("Category"), category.id);
    await user.selectOptions(screen.getByLabelText("Availability"), "false");
    await user.selectOptions(screen.getByLabelText("Visibility"), "false");
    await user.click(screen.getByLabelText("Include deleted"));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => {
          const url = String(input);
          return (
            url.includes(`categoryId=${category.id}`) &&
            url.includes("isAvailable=false") &&
            url.includes("isVisible=false") &&
            url.includes("includeDeleted=true")
          );
        }),
      ).toBe(true),
    );

    await user.click(screen.getAllByRole("button", { name: "Make unavailable" })[0]);
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === "PATCH")).toBe(true),
    );
    await user.click(screen.getByRole("button", { name: "Move Cappuccino down" }));
    const reorder = await waitFor(() =>
      fetchMock.mock.calls.find(
        ([input, init]) =>
          String(input).endsWith("admin/products/reorder") && init?.method === "PUT",
      ),
    );
    expect(String(reorder?.[1]?.body)).toContain(`"categoryId":"${category.id}"`);

    await user.click(screen.getAllByRole("button", { name: "Duplicate" })[0]);
    const dialog = screen.getByRole("dialog", { name: /duplicate cappuccino/i });
    await user.clear(within(dialog).getByLabelText("New product name"));
    await user.type(within(dialog).getByLabelText("New product name"), "Copy");
    await user.click(within(dialog).getByRole("button", { name: "Duplicate" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input).includes("/duplicate") && init?.method === "POST",
        ),
      ).toBe(true),
    );
  });

  it("previews, uploads, assigns, and removes an image through the backend", async () => {
    const user = userEvent.setup();
    const media = {
      id: "55555555-5555-5555-5555-555555555555",
      originalFileName: "drink.png",
      contentType: "image/png",
      fileSizeBytes: 128,
      width: 32,
      height: 32,
      url: "/media/aa/bb/image.png",
      createdAt: "2026-08-06T10:00:00Z",
    };
    const assigned = {
      ...product,
      imageId: media.id,
      image: {
        ...media,
        storageProvider: "Local",
        storageKey: "aa/bb/image.png",
        isDeleted: false,
      },
      rowVersion: "dddddddd-dddd-dddd-dddd-dddddddddddd",
    };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET" && url.includes("admin/categories")) {
          return json(pageOf([category]));
        }
        if (method === "GET" && url.includes("admin/option-groups")) {
          return json(pageOf([]));
        }
        if (method === "GET" && url.includes(`admin/products/${product.id}`)) {
          return json(product);
        }
        if (method === "POST" && url.includes("admin/media/images")) return json(media, 201);
        if (method === "PUT" && url.endsWith(`/products/${product.id}/image`)) {
          const removing = String(init?.body).includes('"imageId":null');
          return json({
            resource: removing
              ? { ...product, rowVersion: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee" }
              : assigned,
            orderability: product.orderability,
          });
        }
        throw new Error(`Unexpected request: ${method} ${url}`);
      },
    );
    renderPage(
      <ProductFormPage />,
      `/staff/menu/products/${product.id}`,
      "/staff/menu/products/:id",
    );

    expect(await screen.findByText("Draft — configuration incomplete")).toBeVisible();
    const file = new File([new Uint8Array([137, 80, 78, 71])], "drink.png", {
      type: "image/png",
    });
    await user.upload(screen.getByLabelText(/JPEG, PNG, or WebP/i), file);
    expect(screen.getByAltText("Preview for Cappuccino")).toHaveAttribute(
      "src",
      "blob:test-preview",
    );
    await user.click(screen.getByRole("button", { name: "Upload image" }));

    expect(await screen.findByText(/drink.png - 32 x 32px/i)).toBeVisible();
    const uploadCall = fetchMock.mock.calls.find(
      ([input, init]) =>
        String(input).includes("admin/media/images") && init?.method === "POST",
    );
    expect(uploadCall?.[1]?.body).toBeInstanceOf(FormData);
    expect(
      fetchMock.mock.calls.some(
        ([input, init]) =>
          String(input).endsWith(`/products/${product.id}/image`) &&
          init?.method === "PUT" &&
          String(init.body).includes(media.id),
      ),
    ).toBe(true);

    await user.click(screen.getByRole("button", { name: "Remove from product" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([, init]) =>
            init?.method === "PUT" && String(init.body).includes('"imageId":null'),
        ),
      ).toBe(true),
    );
  });

  it("rejects unsupported and oversized images before making an upload request", async () => {
    const fetchMock = baseProductFetch();
    renderPage(
      <ProductFormPage />,
      `/staff/menu/products/${product.id}`,
      "/staff/menu/products/:id",
    );
    const input = await screen.findByLabelText(/JPEG, PNG, or WebP/i);
    fireEvent.change(input, {
      target: {
        files: [new File(["script"], "unsafe.svg", { type: "image/svg+xml" })],
      },
    });
    expect(screen.getByText("Choose a JPEG, PNG, or WebP image.")).toBeVisible();
    fireEvent.change(input, {
      target: {
        files: [
          new File([new Uint8Array(maximumBytes + 1)], "huge.png", {
            type: "image/png",
          }),
        ],
      },
    });
    expect(screen.getByText(/exceeds the 5 MB upload limit/i)).toBeVisible();
    expect(
      fetchMock.mock.calls.some(([inputUrl]) =>
        String(inputUrl).includes("admin/media/images"),
      ),
    ).toBe(false);
  });

  it("preserves local product edits and surfaces a 409 conflict", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET" && url.includes("admin/categories")) {
          return json(pageOf([category]));
        }
        if (method === "GET" && url.includes("admin/option-groups")) {
          return json(pageOf([]));
        }
        if (method === "GET") return json(product);
        return json(
          {
            title: "Conflict",
            status: 409,
            code: "MENU_VERSION_CONFLICT",
            currentResource: { id: product.id, rowVersion: "new-version" },
          },
          409,
        );
      },
    );
    renderPage(
      <ProductFormPage />,
      `/staff/menu/products/${product.id}`,
      "/staff/menu/products/:id",
    );
    const name = await screen.findByLabelText("Name");
    await user.clear(name);
    await user.type(name, "Locally edited cappuccino");
    await user.click(screen.getByRole("button", { name: "Save product" }));

    expect(await screen.findByText(/another employee changed/i)).toBeVisible();
    expect(name).toHaveValue("Locally edited cappuccino");
    const update = fetchMock.mock.calls.find(
      ([input, init]) =>
        String(input).endsWith(`admin/products/${product.id}`) &&
        init?.method === "PUT",
    );
    expect(String(update?.[1]?.body)).toContain(product.rowVersion);
  });
});

const maximumBytes = 5_242_880;

function baseProductFetch() {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
    const url = String(input);
    if (url.includes("admin/categories")) return json(pageOf([category]));
    if (url.includes("admin/option-groups")) return json(pageOf([]));
    return json(product);
  });
}

function renderPage(
  element: React.ReactNode,
  initialEntry: string,
  path = initialEntry,
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes>
            <Route path={path} element={element} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

function pageOf(items: unknown[]) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": status >= 400 ? "application/problem+json" : "application/json",
    },
  });
}
