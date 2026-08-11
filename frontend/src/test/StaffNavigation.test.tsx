import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../app/AuthProvider";
import { createQueryClient } from "../app/AppProviders";
import { router } from "../app/router";
import { StaffRoute } from "../components/StaffRoute";
import { ToastProvider } from "../components/ToastProvider";
import { StaffLayout } from "../layouts/StaffLayout";

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

vi.mock("../hooks/useStaffOrderNotifications", () => ({
  useStaffOrderNotifications: () => "Connected",
}));

describe("staff authorization and navigation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.logoutSession.mockResolvedValue(undefined);
  });

  it("redirects an unauthenticated visitor to staff login without flashing staff content", async () => {
    mocks.refreshAccessToken.mockResolvedValue(null);
    renderStaff("/staff/menu", <h1>Protected menu</h1>, "manageMenu");

    expect(await screen.findByRole("heading", { name: "Staff login" })).toBeVisible();
    expect(screen.queryByText("Protected menu")).not.toBeInTheDocument();
  });

  it("shows forbidden to an authenticated customer", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      token({ sub: "customer-1", account_type: "customer" }),
    );
    renderStaff("/staff/menu", <h1>Protected menu</h1>, "manageMenu");

    expect(
      await screen.findByRole("heading", { name: /not available to this account/i }),
    ).toBeVisible();
    expect(screen.queryByText("Protected menu")).not.toBeInTheDocument();
  });

  it("lets a MenuManager see Menu but hides Audit log", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["MenuManager"], "Mina Manager"),
    );
    renderLayout();

    expect(await screen.findByRole("link", { name: "Menu overview" })).toBeVisible();
    expect(screen.queryByRole("link", { name: "Audit log" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Employees" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Orders" })).not.toBeInTheDocument();
    expect(screen.getByText("Mina Manager")).toBeVisible();
  });

  it("shows Orders to a Cashier without exposing menu administration", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["Cashier"], "Cathy Cashier"),
    );
    renderLayout();

    expect(await screen.findByRole("link", { name: "Orders" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Kitchen" })).toBeVisible();
    expect(screen.queryByRole("link", { name: "Menu overview" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Audit log" })).not.toBeInTheDocument();
  });

  it("shows menu and audit navigation to an Administrator", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["Administrator"], "Ada Admin"),
    );
    renderLayout();

    expect(await screen.findByRole("link", { name: "Menu overview" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Audit log" })).toBeVisible();
    expect(screen.getByRole("link", { name: "Employees" })).toBeVisible();
    expect(screen.getByText("Administrator")).toBeVisible();
  });

  it("forbids an employee without the menu capability", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["Kitchen"], "Kira Kitchen"),
    );
    renderStaff("/staff/menu", <h1>Protected menu</h1>, "manageMenu");

    expect(
      await screen.findByRole("heading", { name: /not available to this account/i }),
    ).toBeVisible();
  });

  it("forbids employee management to non-Administrators", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["Manager"], "Manny Manager"),
    );
    renderStaff("/staff/menu", <h1>Employee management</h1>, "manageEmployees");

    expect(
      await screen.findByRole("heading", { name: /not available to this account/i }),
    ).toBeVisible();
    expect(screen.queryByText("Employee management")).not.toBeInTheDocument();
  });

  it("redirects a temporary-password employee to the password-change profile", async () => {
    mocks.refreshAccessToken.mockResolvedValue(
      employeeToken(["Kitchen"], "Kira Kitchen", true),
    );
    renderStaff("/staff/menu", <h1>Staff functions</h1>, "employee");

    expect(await screen.findByRole("heading", { name: "Change password" })).toBeVisible();
    expect(screen.queryByText("Staff functions")).not.toBeInTheDocument();
  });

  it("mounts order management inside the staff workspace", () => {
    const staffRoot = router.routes.find((route) => route.path === "/staff");
    const customerRoot = router.routes.find((route) => route.path === "/");

    expect(staffRoot?.children?.map((route) => route.path)).toEqual(
      expect.arrayContaining([
        "orders",
        "orders/:id",
        "kitchen",
        "employees",
        "employees/new",
        "employees/:id",
      ]),
    );
    expect(
      customerRoot?.children?.filter((route) => route.path === "orders"),
    ).toHaveLength(1);
  });
});

function renderLayout() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={["/staff"]}>
            <Routes>
              <Route
                path="/staff"
                element={
                  <StaffRoute>
                    <StaffLayout />
                  </StaffRoute>
                }
              >
                <Route index element={<h1>Staff dashboard</h1>} />
              </Route>
            </Routes>
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function renderStaff(
  initialEntry: string,
  children: ReactNode,
  capability:
    | "employee"
    | "manageMenu"
    | "manageOrders"
    | "manageEmployees"
    | "viewAuditLog",
) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AuthProvider>
        <ToastProvider>
          <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
              <Route
                path="/staff/menu"
                element={<StaffRoute capability={capability}>{children}</StaffRoute>}
              />
              <Route path="/staff/login" element={<h1>Staff login</h1>} />
              <Route path="/staff/profile" element={<h1>Change password</h1>} />
            </Routes>
          </MemoryRouter>
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function employeeToken(roles: string[], name: string, mustChangePassword = false) {
  return token({
    sub: "employee-1",
    account_type: "employee",
    unique_name: "employee",
    name,
    roles,
    must_change_password: String(mustChangePassword),
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
