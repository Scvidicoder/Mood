import { QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../app/AuthProvider";
import { createQueryClient } from "../app/AppProviders";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { CustomerLoginPage } from "../pages/CustomerLoginPage";
import { StaffLoginPage } from "../pages/StaffLoginPage";
import { TelegramLinkPage } from "../pages/TelegramLinkPage";
import { VerifyCodePage } from "../pages/VerifyCodePage";

const mocks = vi.hoisted(() => ({
  requestCustomerCode: vi.fn(),
  getCustomerChallengeStatus: vi.fn(),
  verifyCustomerCode: vi.fn(),
  completeCustomerRegistration: vi.fn(),
  loginEmployee: vi.fn(),
  changeEmployeePassword: vi.fn(),
  logoutSession: vi.fn(),
  refreshAccessToken: vi.fn(),
}));

vi.mock("../api/auth", () => ({
  requestCustomerCode: mocks.requestCustomerCode,
  getCustomerChallengeStatus: mocks.getCustomerChallengeStatus,
  verifyCustomerCode: mocks.verifyCustomerCode,
  completeCustomerRegistration: mocks.completeCustomerRegistration,
  loginEmployee: mocks.loginEmployee,
  changeEmployeePassword: mocks.changeEmployeePassword,
  logoutSession: mocks.logoutSession,
}));

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    refreshAccessToken: mocks.refreshAccessToken,
  };
});

describe("authentication routes", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.refreshAccessToken.mockResolvedValue(null);
    mocks.logoutSession.mockResolvedValue(undefined);
    mocks.getCustomerChallengeStatus.mockResolvedValue({
      status: "WaitingForTelegramStart",
      expiresInSeconds: 300,
      canResend: false,
    });
  });

  afterEach(() => {
    window.history.replaceState({}, "", "/");
  });

  it("completes the existing-customer phone and OTP login flow", async () => {
    const user = userEvent.setup();
    mocks.requestCustomerCode.mockResolvedValue({
      challengeId: "challenge-1",
      expiresInSeconds: 300,
      resendAvailableInSeconds: 60,
      telegramBotUrl: "https://t.me/test_bot",
      clientChallengeSecret: "client-secret-1",
      status: "OtpSent",
    });
    mocks.verifyCustomerCode.mockResolvedValue({
      isNewCustomer: false,
      accessToken: createTestToken({
        sub: "customer-1",
        account_type: "customer",
        phone_number: "+992900000001",
      }),
      expiresInSeconds: 900,
      customer: {
        id: "customer-1",
        name: "Amina",
        phoneNumber: "+992900000001",
      },
    });

    renderAuthRoutes(
      "/login",
      <>
        <Route path="/login" element={<CustomerLoginPage />} />
        <Route path="/verify" element={<VerifyCodePage />} />
        <Route path="/profile" element={<h1>Customer profile reached</h1>} />
      </>,
    );

    const phoneInput = screen.getByLabelText("Phone number");
    await user.clear(phoneInput);
    await user.type(phoneInput, "+992900000001");
    await user.click(screen.getByRole("button", { name: "Request Telegram code" }));

    expect(await screen.findByRole("heading", { name: "Enter your code" })).toBeVisible();
    await user.type(screen.getByLabelText("Verification code"), "123456");
    await user.click(screen.getByRole("button", { name: "Verify code" }));

    expect(
      await screen.findByRole("heading", { name: "Customer profile reached" }),
    ).toBeVisible();
    expect(mocks.requestCustomerCode).toHaveBeenCalledWith("+992900000001");
    expect(mocks.verifyCustomerCode).toHaveBeenCalledWith("challenge-1", "123456");
  });

  it("shows the Telegram linking page and uses the backend-provided deep link", async () => {
    const user = userEvent.setup();
    mocks.requestCustomerCode.mockResolvedValue({
      challengeId: "challenge-link",
      expiresInSeconds: 300,
      resendAvailableInSeconds: 60,
      telegramBotUrl: "https://t.me/test_bot?start=opaque-token",
      clientChallengeSecret: "client-secret-link",
      status: "WaitingForTelegramStart",
    });
    mocks.getCustomerChallengeStatus.mockResolvedValue({
      status: "WaitingForTelegramContact",
      expiresInSeconds: 280,
      canResend: false,
    });

    renderAuthRoutes(
      "/login",
      <>
        <Route path="/login" element={<CustomerLoginPage />} />
        <Route path="/login/telegram" element={<TelegramLinkPage />} />
        <Route path="/verify" element={<VerifyCodePage />} />
      </>,
    );

    const phoneInput = screen.getByLabelText("Phone number");
    await user.clear(phoneInput);
    await user.type(phoneInput, "+992900000010");
    await user.click(screen.getByRole("button", { name: "Request Telegram code" }));

    expect(
      await screen.findByRole("heading", { name: "Link your phone securely" }),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "Open Telegram" })).toHaveAttribute(
      "href",
      "https://t.me/test_bot?start=opaque-token",
    );
    expect(
      await screen.findByText("The bot is waiting for your shared contact."),
    ).toBeVisible();
    expect(mocks.getCustomerChallengeStatus).toHaveBeenCalledWith(
      "challenge-link",
      "client-secret-link",
    );
    expect(JSON.stringify(window.localStorage)).not.toContain(
      "client-secret-link",
    );
  });

  it("moves from Telegram status polling to OTP verification", async () => {
    const user = userEvent.setup();
    mocks.requestCustomerCode.mockResolvedValue({
      challengeId: "challenge-ready",
      expiresInSeconds: 300,
      resendAvailableInSeconds: 60,
      telegramBotUrl: "https://t.me/test_bot?start=opaque-ready",
      clientChallengeSecret: "client-secret-ready",
      status: "WaitingForTelegramStart",
    });
    mocks.getCustomerChallengeStatus.mockResolvedValue({
      status: "OtpSent",
      expiresInSeconds: 299,
      canResend: false,
    });

    renderAuthRoutes(
      "/login",
      <>
        <Route path="/login" element={<CustomerLoginPage />} />
        <Route path="/login/telegram" element={<TelegramLinkPage />} />
        <Route path="/verify" element={<VerifyCodePage />} />
      </>,
    );

    const phoneInput = screen.getByLabelText("Phone number");
    await user.clear(phoneInput);
    await user.type(phoneInput, "+992900000011");
    await user.click(screen.getByRole("button", { name: "Request Telegram code" }));

    expect(
      await screen.findByRole("heading", { name: "Enter your code" }),
    ).toBeVisible();
  });

  it("returns safely to phone entry when Telegram route state is missing", async () => {
    renderAuthRoutes(
      "/login/telegram",
      <>
        <Route path="/login" element={<h1>Phone entry</h1>} />
        <Route path="/login/telegram" element={<TelegramLinkPage />} />
      </>,
    );

    expect(
      await screen.findByRole("heading", { name: "Start with your phone number" }),
    ).toBeVisible();
    expect(
      screen.getByRole("link", { name: "Return to sign in" }),
    ).toHaveAttribute("href", "/login");
  });

  it("completes employee login and reaches the protected staff destination", async () => {
    const user = userEvent.setup();
    mocks.loginEmployee.mockResolvedValue({
      accessToken: createTestToken({
        sub: "employee-1",
        account_type: "employee",
        unique_name: "admin",
        roles: ["Administrator"],
        must_change_password: "false",
      }),
      expiresInSeconds: 900,
      mustChangePassword: false,
      employee: {
        id: "employee-1",
        fullName: "Administrator",
        username: "admin",
        roles: ["Administrator"],
      },
    });

    renderAuthRoutes(
      "/staff/login",
      <>
        <Route path="/staff/login" element={<StaffLoginPage />} />
        <Route path="/staff" element={<h1>Staff destination reached</h1>} />
      </>,
    );

    await user.type(screen.getByLabelText("Username"), "admin");
    await user.type(screen.getByLabelText("Password"), "TestingAdmin1!");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(
      await screen.findByRole("heading", { name: "Staff destination reached" }),
    ).toBeVisible();
    expect(mocks.loginEmployee).toHaveBeenCalledWith("admin", "TestingAdmin1!");
  });

  it("redirects an unauthenticated customer away from a protected route", async () => {
    renderAuthRoutes(
      "/profile",
      <>
        <Route
          path="/profile"
          element={
            <ProtectedRoute accountType="customer">
              <h1>Private profile</h1>
            </ProtectedRoute>
          }
        />
        <Route path="/login" element={<h1>Login required</h1>} />
      </>,
    );

    expect(await screen.findByRole("heading", { name: "Login required" })).toBeVisible();
    expect(screen.queryByRole("heading", { name: "Private profile" })).not.toBeInTheDocument();
  });
});

function renderAuthRoutes(initialEntry: string, routes: ReactNode) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes>{routes}</Routes>
        </MemoryRouter>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function createTestToken(payload: Record<string, unknown>): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value))
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");

  return `${encode({ alg: "none", typ: "JWT" })}.${encode(payload)}.test`;
}
