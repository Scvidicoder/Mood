import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../components/ToastProvider";
import { AuditLogDetailPage } from "../pages/staff/audit/AuditLogDetailPage";
import { AuditLogPage } from "../pages/staff/audit/AuditLogPage";

const entry = {
  id: "11111111-1111-1111-1111-111111111111",
  timestamp: "2026-08-06T10:00:00Z",
  employeeId: "22222222-2222-2222-2222-222222222222",
  employeeName: "Ada Admin",
  actionType: "Updated",
  entityType: "Product",
  entityId: "33333333-3333-3333-3333-333333333333",
  description: "Updated product Cappuccino.",
  correlationId: "corr-123",
};

describe("audit log administration", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("loads history and sends list filters through the API client", async () => {
    const user = userEvent.setup();
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(json(pageOf([entry])));
    renderRoute(<AuditLogPage />, "/staff/audit-log");

    expect(await screen.findByText("Updated product Cappuccino.")).toBeVisible();
    await user.type(screen.getByLabelText("Employee ID"), entry.employeeId);
    await user.type(screen.getByLabelText("Action type"), "Updated");
    await user.type(screen.getByLabelText("Entity type"), "Product");
    await user.type(screen.getByLabelText("Entity ID"), entry.entityId);
    await user.type(screen.getByLabelText("From"), "2026-08-01");
    await user.type(screen.getByLabelText("To"), "2026-08-06");

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([input]) => {
          const url = String(input);
          return (
            url.includes(`employeeId=${entry.employeeId}`) &&
            url.includes("actionType=Updated") &&
            url.includes("entityType=Product") &&
            url.includes(`entityId=${entry.entityId}`) &&
            url.includes("dateFrom=") &&
            url.includes("dateTo=")
          );
        }),
      ).toBe(true),
    );
  });

  it("renders changed fields and untrusted JSON as inert text", async () => {
    const malicious = "<img src=x onerror=window.__auditXss=true>";
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      json({
        ...entry,
        oldValuesJson: JSON.stringify({ name: "Old", description: null }),
        newValuesJson: JSON.stringify({ name: malicious, description: "Safe" }),
      }),
    );
    renderRoute(
      <AuditLogDetailPage />,
      `/staff/audit-log/${entry.id}`,
      "/staff/audit-log/:id",
    );

    expect(await screen.findByRole("heading", { name: "Changed fields" })).toBeVisible();
    expect(screen.getAllByText("name").length).toBeGreaterThan(0);
    expect(screen.getAllByText(new RegExp("onerror")).length).toBeGreaterThan(0);
    expect(document.querySelector("img")).toBeNull();
    expect((window as Window & { __auditXss?: boolean }).__auditXss).toBeUndefined();
    expect(screen.getByText("corr-123")).toBeVisible();
  });
});

function renderRoute(
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
    headers: { "Content-Type": "application/json" },
  });
}
