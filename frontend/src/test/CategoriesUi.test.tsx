import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { CategoriesPage } from "../pages/staff/menu/CategoriesPage";
import { CategoryFormPage } from "../pages/staff/menu/CategoryFormPage";

const category = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "Coffee",
  description: "Hot drinks",
  displayOrder: 0,
  isVisible: true,
  isDeleted: false,
  productCount: 3,
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: "2026-08-02T10:00:00Z",
  rowVersion: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
};

const deletedCategory = {
  ...category,
  id: "22222222-2222-2222-2222-222222222222",
  name: "Archive",
  displayOrder: 1,
  isVisible: false,
  isDeleted: true,
  productCount: 0,
  rowVersion: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
};

describe("category administration", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("shows a loading state while the network request is pending", () => {
    vi.spyOn(globalThis, "fetch").mockReturnValue(new Promise(() => undefined));
    renderPage(<CategoriesPage />, "/staff/menu/categories");
    expect(screen.getByText(/loading categories/i)).toBeVisible();
  });

  it("loads, toggles, deletes, restores, and reorders through the HTTP boundary", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET" && url.includes("admin/categories")) {
          return json({
            items: [category, deletedCategory],
            page: 1,
            pageSize: 20,
            totalCount: 2,
            totalPages: 1,
          });
        }
        if (method === "PATCH") return json({ ...category, isVisible: false });
        if (method === "PUT" && url.endsWith("admin/categories/reorder")) {
          return json([deletedCategory, category]);
        }
        if (method === "DELETE") return new Response(null, { status: 204 });
        if (method === "POST" && url.includes("/restore")) {
          return json({ ...deletedCategory, isDeleted: false });
        }
        throw new Error(`Unexpected request: ${method} ${url}`);
      },
    );
    renderPage(<CategoriesPage />, "/staff/menu/categories");

    expect(await screen.findByText("Coffee")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Hide" }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        expect.objectContaining({ href: expect.stringContaining("/visibility") }),
        expect.objectContaining({ method: "PATCH" }),
      ),
    );

    await user.click(screen.getByRole("button", { name: "Move Coffee down" }));
    const reorderCall = fetchMock.mock.calls.find(
      ([input, init]) =>
        String(input).endsWith("admin/categories/reorder") && init?.method === "PUT",
    );
    expect(reorderCall).toBeDefined();
    expect(String(reorderCall?.[1]?.body)).toContain(category.rowVersion);

    await user.click(screen.getByRole("button", { name: "Delete" }));
    const deleteDialog = screen.getByRole("dialog");
    await user.click(within(deleteDialog).getByRole("button", { name: "Delete" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([, init]) => init?.method === "DELETE"),
      ).toBe(true),
    );

    await user.click(screen.getByRole("button", { name: "Restore" }));
    const restoreDialog = screen.getByRole("dialog");
    await user.click(within(restoreDialog).getByRole("button", { name: "Restore" }));
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([input, init]) =>
            String(input).includes("/restore") && init?.method === "POST",
        ),
      ).toBe(true),
    );
  });

  it("validates create input and maps ProblemDetails field errors", async () => {
    const user = userEvent.setup();
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      json(
        {
          title: "Validation failed",
          status: 400,
          errors: { Name: ["A server-side name error."] },
        },
        400,
      ),
    );
    renderPage(<CategoryFormPage />, "/staff/menu/categories/new", true);

    expect(screen.getByText("Name is required.")).toBeVisible();
    await user.type(screen.getByLabelText("Name"), "Coffee");
    await user.click(screen.getByRole("button", { name: "Save category" }));
    expect(await screen.findByText("A server-side name error.")).toBeVisible();
  });

  it("updates with rowVersion and displays a dedicated 409 conflict", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (_input, init) => {
        if ((init?.method ?? "GET") === "GET") return json(category);
        return json(
          {
            title: "Menu version conflict",
            status: 409,
            code: "MENU_VERSION_CONFLICT",
            currentResource: {
              id: category.id,
              rowVersion: "cccccccc-cccc-cccc-cccc-cccccccccccc",
            },
          },
          409,
        );
      },
    );
    renderPage(
      <CategoryFormPage />,
      `/staff/menu/categories/${category.id}`,
      false,
      "/staff/menu/categories/:id",
    );

    const name = await screen.findByLabelText("Name");
    await user.clear(name);
    await user.type(name, "Fresh coffee");
    await user.click(screen.getByRole("button", { name: "Save category" }));

    expect(await screen.findByText(/another employee changed/i)).toBeVisible();
    const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(String(putCall?.[1]?.body)).toContain(category.rowVersion);
    expect(screen.getByRole("button", { name: "Reload latest" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Discard local changes" })).toBeVisible();
  });
});

function renderPage(
  element: React.ReactNode,
  initialEntry: string,
  includeListRoute = false,
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
            {includeListRoute ? (
              <Route path="/staff/menu/categories/:id" element={<CategoryFormPage />} />
            ) : null}
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });
}
