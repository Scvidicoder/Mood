import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApplicationLayout } from "../layouts/ApplicationLayout";
import { ProfilePage } from "../pages/ProfilePage";
import { createAppStore } from "../store";

const mocks = vi.hoisted(() => ({
  getProfile: vi.fn(),
  updateProfile: vi.fn(),
  logout: vi.fn(),
}));

vi.mock("../api/profile", () => ({
  getProfile: mocks.getProfile,
  updateProfile: mocks.updateProfile,
}));
vi.mock("../app/AuthProvider", () => ({
  useAuth: () => ({
    session: {
      accountType: "customer",
      accountId: "customer-internal-id",
      phoneNumber: "+992900000001",
    },
    logout: mocks.logout,
  }),
}));
vi.mock("../hooks/useOrderNotifications", () => ({
  useOrderNotifications: () => "Connected",
}));

describe("customer profile", () => {
  beforeEach(() => {
    mocks.getProfile.mockReset();
    mocks.updateProfile.mockReset();
    mocks.logout.mockReset();
    mocks.getProfile.mockResolvedValue(profile());
  });

  it("shows safe account information and edits only the customer name", async () => {
    mocks.updateProfile.mockResolvedValue({
      ...profile(),
      name: "Updated Customer",
      rowVersion: "version-2",
    });
    renderProfile();

    expect(await screen.findByRole("heading", { name: "Amina" })).toBeVisible();
    expect(screen.getByText("+992900000001")).toBeVisible();
    expect(screen.getByText("Verified")).toBeVisible();
    expect(screen.getByText("Linked")).toBeVisible();
    expect(screen.getByText("2")).toBeVisible();
    expect(screen.queryByText("customer-internal-id")).not.toBeInTheDocument();
    expect(screen.queryByText(/money spent/i)).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Customer name"), {
      target: { value: "Updated Customer" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save name" }));

    await waitFor(() => {
      expect(mocks.updateProfile).toHaveBeenCalledWith({
        name: "Updated Customer",
        rowVersion: "version-1",
      });
    });
    expect(await screen.findByText("Your name was updated.")).toBeVisible();
  });

  it("links authenticated navigation to the profile order history", () => {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    render(
      <QueryClientProvider client={client}>
        <Provider store={createAppStore(undefined)}>
          <MemoryRouter initialEntries={["/"]}>
            <Routes>
              <Route element={<ApplicationLayout />} path="/">
                <Route index element={<p>Menu content</p>} />
              </Route>
            </Routes>
          </MemoryRouter>
        </Provider>
      </QueryClientProvider>,
    );

    expect(screen.getAllByRole("link", { name: "My orders" })[0])
      .toHaveAttribute("href", "/profile/orders");
  });
});

function renderProfile() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ProfilePage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function profile() {
  return {
    name: "Amina",
    phoneNumber: "+992900000001",
    phoneVerified: true,
    telegramLinked: true,
    registrationDate: "2026-08-01T08:00:00.000Z",
    activeOrderCount: 2,
    completedOrderCount: 3,
    rowVersion: "version-1",
  };
}
