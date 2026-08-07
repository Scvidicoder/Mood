import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { ProductOptionsEditor } from "../pages/staff/menu/ProductOptionsEditor";
import { OptionGroupFormPage } from "../pages/staff/menu/OptionGroupFormPage";

const globalValue = {
  id: "11111111-1111-1111-1111-111111111111",
  optionGroupId: "22222222-2222-2222-2222-222222222222",
  name: "Small",
  description: "250 ml",
  displayOrder: 0,
  isActive: true,
  isDeleted: false,
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: "2026-08-01T10:00:00Z",
  rowVersion: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
};

const group = {
  id: globalValue.optionGroupId,
  name: "Size",
  description: "Drink size",
  selectionType: "Single" as const,
  defaultIsRequired: true,
  defaultMinimumSelections: 1,
  defaultMaximumSelections: 1,
  displayOrder: 0,
  isActive: true,
  isDeleted: false,
  createdAt: "2026-08-01T10:00:00Z",
  updatedAt: "2026-08-01T10:00:00Z",
  rowVersion: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  values: [globalValue],
};

const draftOrderability = {
  isOrderable: false,
  issues: [
    {
      code: "REQUIRED_GROUP_NO_VALUES",
      message: "Required group Size has no available values.",
    },
  ],
};

describe("option group and value administration", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("validates dynamic selection rules and creates a group", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(json(group, 201));
    renderRoute(<OptionGroupFormPage />, "/staff/menu/option-groups/new");

    expect(screen.getByText("Name is required.")).toBeVisible();
    await user.type(screen.getByLabelText("Name"), "Size");
    await user.selectOptions(screen.getByLabelText("Selection type"), "Multiple");
    const maximum = screen.getByLabelText("Default maximum selections");
    await user.clear(maximum);
    await user.type(maximum, "0");
    expect(screen.getByText("Maximum must be at least one.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Save option group" })).toBeDisabled();
    await user.clear(maximum);
    await user.type(maximum, "2");
    await user.click(screen.getByRole("button", { name: "Save option group" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        expect.objectContaining({ href: expect.stringContaining("admin/option-groups") }),
        expect.objectContaining({ method: "POST" }),
      ),
    );
  });

  it("creates a global value, shows duplicate conflict, and supports value lifecycle actions", async () => {
    const user = userEvent.setup();
    let valuePostCount = 0;
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET" && url.includes("/values")) return json([globalValue]);
        if (method === "GET") return json(group);
        if (method === "POST" && url.endsWith("/values")) {
          valuePostCount += 1;
          return json(
            {
              title: "Duplicate option value",
              detail: "A value with this name already exists.",
              status: 409,
              code: "MENU_NAME_CONFLICT",
            },
            409,
          );
        }
        if (method === "PATCH") return json({ ...globalValue, isActive: false });
        if (method === "DELETE") return new Response(null, { status: 204 });
        if (method === "POST" && url.includes("/restore")) return json(globalValue);
        if (method === "PUT") return json(globalValue);
        throw new Error(`Unexpected request: ${method} ${url}`);
      },
    );
    renderRoute(
      <OptionGroupFormPage />,
      `/staff/menu/option-groups/${group.id}`,
      "/staff/menu/option-groups/:id",
    );

    expect(await screen.findByDisplayValue("Small")).toBeVisible();
    const nameInputs = screen.getAllByLabelText("Name");
    await user.type(nameInputs[1], "Small");
    await user.click(screen.getByRole("button", { name: "Add value" }));
    expect(await screen.findByText("A value with this name already exists.")).toBeVisible();
    expect(valuePostCount).toBe(1);

    await user.click(screen.getByRole("button", { name: "Deactivate" }));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === "PATCH")).toBe(true),
    );
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => init?.method === "DELETE")).toBe(true),
    );
  });
});

describe("product option configuration", () => {
  it("assigns a group and treats accepted draft warnings as successful state", async () => {
    const user = userEvent.setup();
    const onChanged = vi.fn().mockResolvedValue(undefined);
    const product = {
      id: "33333333-3333-3333-3333-333333333333",
      categoryId: "44444444-4444-4444-4444-444444444444",
      categoryName: "Coffee",
      name: "Latte",
      basePrice: 20,
      isAvailable: true,
      isVisible: true,
      isDeleted: false,
      displayOrder: 0,
      createdAt: "2026-08-01T10:00:00Z",
      updatedAt: "2026-08-01T10:00:00Z",
      rowVersion: "cccccccc-cccc-cccc-cccc-cccccccccccc",
      orderability: draftOrderability,
      optionGroups: [],
    };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        const method = init?.method ?? "GET";
        if (method === "GET") return json(pageOf([group]));
        if (method === "POST" && url.endsWith("/option-groups")) {
          return json(
            {
              resource: {
                id: "55555555-5555-5555-5555-555555555555",
                optionGroupId: group.id,
                optionGroupName: group.name,
                selectionType: "Single",
                isRequired: true,
                minimumSelections: 1,
                maximumSelections: 1,
                displayOrder: 0,
                isActive: true,
                optionGroupIsActive: true,
                optionGroupIsDeleted: false,
                createdAt: "2026-08-06T10:00:00Z",
                updatedAt: "2026-08-06T10:00:00Z",
                rowVersion: "dddddddd-dddd-dddd-dddd-dddddddddddd",
                values: [],
              },
              orderability: draftOrderability,
            },
            201,
          );
        }
        throw new Error(`Unexpected request: ${method} ${url}`);
      },
    );
    renderComponent(
      <ProductOptionsEditor product={product} onChanged={onChanged} />,
    );

    await user.selectOptions(await screen.findByLabelText("Global option group"), group.id);
    await user.click(screen.getByRole("button", { name: "Assign group" }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledWith(draftOrderability));
    expect(
      screen.getByText(/add only the values this product supports/i),
    ).toBeVisible();
    expect(
      fetchMock.mock.calls.some(
        ([input, init]) =>
          String(input).endsWith("/option-groups") && init?.method === "POST",
      ),
    ).toBe(true);
  });

  it("adds an allowed value, configures its modifier/default, and surfaces wrong-group errors", async () => {
    const user = userEvent.setup();
    const assignmentValue = {
      id: "66666666-6666-6666-6666-666666666666",
      optionValueId: globalValue.id,
      optionValueName: globalValue.name,
      priceModifier: 2.5,
      isDefault: false,
      isAvailable: true,
      displayOrder: 0,
      volumeMilliliters: 250,
      calories: 80,
      optionValueIsActive: true,
      optionValueIsDeleted: false,
      createdAt: "2026-08-06T10:00:00Z",
      updatedAt: "2026-08-06T10:00:00Z",
      rowVersion: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
    };
    const assignment = {
      id: "55555555-5555-5555-5555-555555555555",
      optionGroupId: group.id,
      optionGroupName: group.name,
      selectionType: "Single" as const,
      isRequired: true,
      minimumSelections: 1,
      maximumSelections: 1,
      displayOrder: 0,
      isActive: true,
      optionGroupIsActive: true,
      optionGroupIsDeleted: false,
      createdAt: "2026-08-06T10:00:00Z",
      updatedAt: "2026-08-06T10:00:00Z",
      rowVersion: "dddddddd-dddd-dddd-dddd-dddddddddddd",
      values: [],
    };
    const product = {
      id: "33333333-3333-3333-3333-333333333333",
      categoryId: "44444444-4444-4444-4444-444444444444",
      categoryName: "Coffee",
      name: "Latte",
      basePrice: 20,
      isAvailable: true,
      isVisible: true,
      isDeleted: false,
      displayOrder: 0,
      createdAt: "2026-08-01T10:00:00Z",
      updatedAt: "2026-08-01T10:00:00Z",
      rowVersion: "cccccccc-cccc-cccc-cccc-cccccccccccc",
      orderability: draftOrderability,
      optionGroups: [assignment],
    };
    const onChanged = vi.fn().mockResolvedValue(undefined);
    let valuePost = 0;
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      const method = init?.method ?? "GET";
      if (method === "GET") return json(pageOf([group]));
      if (method === "POST" && url.endsWith("/values")) {
        valuePost += 1;
        if (valuePost === 1) {
          return json(
            { resource: assignmentValue, orderability: draftOrderability },
            201,
          );
        }
        return json(
          {
            title: "Wrong group",
            detail: "The option value belongs to another group.",
            status: 409,
            code: "MENU_OPTION_VALUE_WRONG_GROUP",
          },
          409,
        );
      }
      if (method === "PUT") {
        return json({ resource: { ...assignmentValue, isDefault: true }, orderability: draftOrderability });
      }
      throw new Error(`Unexpected request: ${method} ${url}`);
    });
    renderComponent(<ProductOptionsEditor product={product} onChanged={onChanged} />);

    await screen.findByRole("option", { name: "Small" });
    await user.selectOptions(screen.getByLabelText("Global value"), globalValue.id);
    await user.click(screen.getByRole("button", { name: "Add value" }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledWith(draftOrderability));
  });
});

function renderRoute(
  element: React.ReactNode,
  initialEntry: string,
  path = initialEntry,
) {
  return renderComponent(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path={path} element={element} />
        <Route path="/staff/menu/option-groups/:id" element={element} />
      </Routes>
    </MemoryRouter>,
    false,
  );
}

function renderComponent(element: React.ReactNode, addRouter = true) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        {addRouter ? <MemoryRouter>{element}</MemoryRouter> : element}
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
