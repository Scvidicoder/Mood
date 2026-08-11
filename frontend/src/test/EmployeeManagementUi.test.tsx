import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../app/AuthProvider";
import { ToastProvider } from "../components/ToastProvider";
import { CreateEmployeePage } from "../pages/staff/employees/CreateEmployeePage";
import { EmployeeDetailPage } from "../pages/staff/employees/EmployeeDetailPage";
import { EmployeeListPage } from "../pages/staff/employees/EmployeeListPage";

const mocks = vi.hoisted(() => ({
  refreshAccessToken: vi.fn(),
  logoutSession: vi.fn(),
}));

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return { ...actual, refreshAccessToken: mocks.refreshAccessToken };
});

vi.mock("../api/auth", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/auth")>();
  return { ...actual, logoutSession: mocks.logoutSession };
});

const employee = {
  id: "11111111-1111-1111-1111-111111111111",
  fullName: "Aziz Karimov",
  username: "aziz.k",
  roles: ["Kitchen", "Pickup"],
  isActive: true,
  mustChangePassword: true,
  createdAt: "2026-08-11T08:00:00Z",
  updatedAt: "2026-08-11T08:00:00Z",
  lastLoginAt: undefined,
  rowVersion: "22222222-2222-2222-2222-222222222222",
};

const roles = [
  { name: "Administrator", displayName: "Administrator" },
  { name: "Kitchen", displayName: "Kitchen" },
  { name: "Pickup", displayName: "Pickup" },
];

const permissionResponse = {
  employeeId: employee.id,
  permissions: [
    {
      permission: "ViewOrders",
      displayName: "View Orders",
      group: "Orders",
      roleAllowed: true,
      isAllowed: true,
    },
    {
      permission: "RejectOrders",
      displayName: "Reject Orders",
      group: "Orders",
      roleAllowed: true,
      isAllowed: true,
    },
    {
      permission: "ManageEmployees",
      displayName: "Manage Employees",
      group: "Employees",
      roleAllowed: false,
      isAllowed: false,
    },
  ],
};

describe("employee management administration", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mocks.refreshAccessToken.mockResolvedValue(administratorToken());
    mocks.logoutSession.mockResolvedValue(undefined);
  });

  it("loads, searches, filters, paginates, and renders responsive employee rows", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes("admin/roles")) return json(roles);
      if (url.includes("admin/employees")) {
        const page = new URL(url).searchParams.get("page") ?? "1";
        return json({
          items: [{ ...employee, fullName: page === "2" ? "Second Page" : employee.fullName }],
          page: Number(page),
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    renderEmployeeRoute(<EmployeeListPage />, "/staff/employees");

    expect(await screen.findByText("Aziz Karimov")).toBeVisible();
    expect(screen.getByText("Change required")).toBeVisible();
    expect(screen.getByRole("link", { name: "Create employee" })).toBeVisible();
    expect(screen.getByText("Never")).toBeVisible();
    expect(screen.getByRole("cell", { name: /Aziz Karimov/i })).toHaveAttribute(
      "data-label",
      "Employee",
    );

    await user.type(screen.getByLabelText("Search"), "aziz");
    await user.selectOptions(screen.getByLabelText("Role"), "Kitchen");
    await user.selectOptions(screen.getByLabelText("Status"), "Active");
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => {
          const url = String(input);
          return url.includes("search=aziz") &&
            url.includes("role=Kitchen") &&
            url.includes("status=Active");
        }),
      ).toBe(true),
    );
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(await screen.findByText("Second Page")).toBeVisible();
  });

  it("creates an employee with multiple roles and shows the password only in local page state", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(
      async (input, init) => {
        const url = String(input);
        if (url.includes("admin/roles")) return json(roles);
        if (url.endsWith("admin/employees") && init?.method === "POST") {
          return json(
            {
              employee,
              temporaryPassword: "TempEmployee2!Abcd",
            },
            201,
          );
        }
        throw new Error(`Unexpected request: ${url}`);
      },
    );
    renderEmployeeRoute(<CreateEmployeePage />, "/staff/employees/new");

    await screen.findByRole("heading", { name: "Create employee" });
    await user.type(screen.getByLabelText("Full name"), "Aziz Karimov");
    await user.type(screen.getByLabelText("Username"), "aziz.k");
    await user.click(screen.getByLabelText("Kitchen"));
    await user.click(screen.getByLabelText("Pickup"));
    await user.click(screen.getByRole("button", { name: "Create employee" }));

    expect(await screen.findByText("TempEmployee2!Abcd")).toBeVisible();
    expect(screen.getByText(/shown only once/i)).toBeVisible();
    const createCall = fetchMock.mock.calls.find(
      ([input, init]) => String(input).endsWith("admin/employees") && init?.method === "POST",
    );
    expect(JSON.parse(String(createCall?.[1]?.body))).toMatchObject({
      fullName: "Aziz Karimov",
      username: "aziz.k",
      roles: ["Kitchen", "Pickup"],
    });
    await user.click(screen.getByRole("button", { name: "Copy password" }));
    expect(screen.getByRole("button", { name: "Copied" })).toBeVisible();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it("updates roles, reports concurrency, resets passwords, and shows action history", async () => {
    const user = userEvent.setup();
    let updateAttempt = 0;
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.includes("admin/roles")) return json(roles);
      if (url.includes(`/admin/employees/${employee.id}/permissions`)) {
        return json(permissionResponse);
      }
      if (url.includes(`/admin/employees/${employee.id}/actions`)) {
        return json(pageOf([
          {
            id: "action-1",
            timestamp: "2026-08-11T09:00:00Z",
            actingEmployeeId: "admin-id",
            actingEmployeeName: "Ada Admin",
            actionType: "EmployeeCreated",
            entityType: "Employee",
            entityId: employee.id,
            description: "Created employee aziz.k.",
            correlationId: "corr-1",
          },
        ]));
      }
      if (url.endsWith(`/admin/employees/${employee.id}`) && (!init?.method || init.method === "GET")) {
        return json(employee);
      }
      if (url.endsWith(`/admin/employees/${employee.id}`) && init?.method === "PUT") {
        updateAttempt += 1;
        if (updateAttempt === 2) {
          return problem(
            "Employee was changed by another administrator",
            "EMPLOYEE_VERSION_CONFLICT",
          );
        }
        return json({
          ...employee,
          fullName: "Aziz Lead",
          roles: ["Kitchen"],
          mustChangePassword: false,
          rowVersion: "33333333-3333-3333-3333-333333333333",
        });
      }
      if (url.endsWith(`/admin/employees/${employee.id}/reset-password`)) {
        return json({
          temporaryPassword: "ResetEmployee3!Abc",
          mustChangePassword: true,
          rowVersion: "44444444-4444-4444-4444-444444444444",
          revokedSessionCount: 2,
        });
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    renderEmployeeRoute(
      <EmployeeDetailPage />,
      `/staff/employees/${employee.id}`,
      "/staff/employees/:id",
    );

    expect(await screen.findByText("Created employee aziz.k.")).toBeVisible();
    const name = screen.getByLabelText("Full name");
    await user.clear(name);
    await user.type(name, "Aziz Lead");
    await user.click(screen.getByLabelText("Pickup"));
    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(await screen.findByText("Password change")).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Save changes" }));
    expect(
      await screen.findByText("Another employee changed this resource."),
    ).toBeVisible();

    await user.click(screen.getByRole("button", { name: "Reset password" }));
    await user.click(
      screen.getAllByRole("button", { name: "Reset password" }).at(-1)!,
    );
    expect(await screen.findByText("ResetEmployee3!Abc")).toBeVisible();
    expect(screen.getByText(/Existing sessions were revoked \(2 active sessions\)/i)).toBeVisible();
  });

  it("shows the last-administrator business conflict instead of a generic error", async () => {
    const user = userEvent.setup();
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.includes("admin/roles")) return json(roles);
      if (url.includes(`/admin/employees/${employee.id}/permissions`)) {
        return json(permissionResponse);
      }
      if (url.includes(`/admin/employees/${employee.id}/actions`)) return json(pageOf([]));
      if (url.endsWith(`/admin/employees/${employee.id}`)) {
        return json({ ...employee, roles: ["Administrator"] });
      }
      if (url.endsWith(`/admin/employees/${employee.id}/disable`) && init?.method === "POST") {
        return problem(
          "At least one active Administrator account must remain",
          "LAST_ADMINISTRATOR_PROTECTION",
        );
      }
      throw new Error(`Unexpected request: ${url}`);
    });
    renderEmployeeRoute(
      <EmployeeDetailPage />,
      `/staff/employees/${employee.id}`,
      "/staff/employees/:id",
    );

    await screen.findByRole("heading", { name: "Aziz Karimov" });
    await user.click(screen.getByRole("button", { name: "Disable access" }));
    await user.click(screen.getByRole("button", { name: "Disable employee" }));
    expect(
      await screen.findByText("At least one active Administrator account must remain"),
    ).toBeVisible();
  });

  it("saves employee permission overrides and resets to role defaults", async () => {
    const user = userEvent.setup();
    const permissionBodies: unknown[] = [];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.includes("admin/roles")) return json(roles);
      if (url.includes(`/admin/employees/${employee.id}/actions`)) return json(pageOf([]));
      if (url.endsWith(`/admin/employees/${employee.id}/permissions`)) {
        if (init?.method === "PUT") {
          const body = JSON.parse(String(init.body));
          permissionBodies.push(body);
          const rejectOverride = body.overrides.find(
            (value: { permission: string }) => value.permission === "RejectOrders",
          );
          return json({
            ...permissionResponse,
            permissions: permissionResponse.permissions.map((permission) =>
              permission.permission === "RejectOrders" && rejectOverride
                ? {
                    ...permission,
                    override: rejectOverride.isAllowed,
                    isAllowed: rejectOverride.isAllowed,
                  }
                : permission,
            ),
          });
        }
        return json(permissionResponse);
      }
      if (url.endsWith(`/admin/employees/${employee.id}`)) return json(employee);
      throw new Error(`Unexpected request: ${url}`);
    });
    renderEmployeeRoute(
      <EmployeeDetailPage />,
      `/staff/employees/${employee.id}`,
      "/staff/employees/:id",
    );

    const rejectOrders = await screen.findByLabelText("Reject Orders");
    expect(rejectOrders).toBeChecked();
    await user.click(rejectOrders);
    await user.click(screen.getByRole("button", { name: "Save permissions" }));
    expect(await screen.findByText("Employee permissions updated.")).toBeVisible();
    expect(permissionBodies[0]).toEqual({
      overrides: [{ permission: "RejectOrders", isAllowed: false }],
    });

    await user.click(screen.getByRole("button", { name: "Reset to Role Defaults" }));
    expect(await screen.findByText("Permissions reset to role defaults.")).toBeVisible();
    expect(permissionBodies[1]).toEqual({ overrides: [] });
  });
});

function renderEmployeeRoute(
  element: React.ReactNode,
  initialEntry: string,
  path = initialEntry,
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
              <Route path={path} element={element} />
            </Routes>
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function pageOf(items: unknown[]) {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function problem(title: string, code: string) {
  return json({ title, code, status: 409 }, 409);
}

function administratorToken() {
  return token({
    sub: "admin-id",
    account_type: "employee",
    unique_name: "admin",
    name: "Ada Admin",
    roles: ["Administrator"],
    must_change_password: "false",
  });
}

function token(payload: Record<string, unknown>): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value))
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");
  return `${encode({ alg: "none", typ: "JWT" })}.${encode(payload)}.test`;
}
