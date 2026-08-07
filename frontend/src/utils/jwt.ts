import type { AuthSession } from "../types/auth";

interface AccessTokenPayload {
  sub?: string;
  account_type?: "customer" | "employee";
  phone_number?: string;
  unique_name?: string;
  name?: string;
  roles?: string | string[];
  must_change_password?: string | boolean;
}

export function sessionFromAccessToken(accessToken: string): AuthSession {
  const parts = accessToken.split(".");
  if (parts.length !== 3) {
    throw new Error("The access token has an invalid format.");
  }

  const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
  const paddedBase64 = base64.padEnd(
    base64.length + ((4 - (base64.length % 4)) % 4),
    "=",
  );
  const payload = JSON.parse(atob(paddedBase64)) as AccessTokenPayload;

  if (!payload.sub || !payload.account_type) {
    throw new Error("The access token is missing required account claims.");
  }

  const roles = Array.isArray(payload.roles)
    ? payload.roles
    : payload.roles
      ? [payload.roles]
      : [];

  return {
    accountId: payload.sub,
    accountType: payload.account_type,
    phoneNumber: payload.phone_number,
    username: payload.unique_name,
    fullName: payload.name,
    roles,
    mustChangePassword:
      payload.must_change_password === true ||
      payload.must_change_password === "true",
  };
}
