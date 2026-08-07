import type {
  CustomerAuthenticationResponse,
  CustomerChallengeStatusResponse,
  CustomerVerificationResponse,
  EmployeeAuthenticationResponse,
  RequestCodeResponse,
} from "../types/auth";
import { environment } from "../utils/environment";
import { apiClient } from "./client";

export function requestCustomerCode(phoneNumber: string): Promise<RequestCodeResponse> {
  return apiClient.post<RequestCodeResponse>(
    "auth/customer/request-code",
    { phoneNumber },
    { retryUnauthorized: false },
  );
}

export function getCustomerChallengeStatus(
  challengeId: string,
  clientChallengeSecret: string,
): Promise<CustomerChallengeStatusResponse> {
  return apiClient.post<CustomerChallengeStatusResponse>(
    "auth/customer/challenge-status",
    { challengeId, clientChallengeSecret },
    { retryUnauthorized: false },
  );
}

export function verifyCustomerCode(
  challengeId: string,
  code: string,
): Promise<CustomerVerificationResponse> {
  return apiClient.post<CustomerVerificationResponse>(
    "auth/customer/verify-code",
    { challengeId, code },
    { retryUnauthorized: false },
  );
}

export function completeCustomerRegistration(
  registrationToken: string,
  name: string,
): Promise<CustomerAuthenticationResponse> {
  return apiClient.post<CustomerAuthenticationResponse>(
    "auth/customer/complete-registration",
    { registrationToken, name },
    { retryUnauthorized: false },
  );
}

export function loginEmployee(
  username: string,
  password: string,
): Promise<EmployeeAuthenticationResponse> {
  return apiClient.post<EmployeeAuthenticationResponse>(
    "staff/auth/login",
    { username, password },
    { retryUnauthorized: false },
  );
}

export function changeEmployeePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  return apiClient.post<void>("staff/auth/change-password", {
    currentPassword,
    newPassword,
  });
}

function readCsrfCookie(): string | undefined {
  const prefix = `${encodeURIComponent(environment.csrfCookieName)}=`;
  const cookie = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));

  return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : undefined;
}

export function logoutSession(): Promise<void> {
  const csrfToken = readCsrfCookie();
  return apiClient.post<void>(
    "auth/logout",
    undefined,
    {
      retryUnauthorized: false,
      headers: csrfToken
        ? { [environment.csrfHeaderName]: csrfToken }
        : undefined,
    },
  );
}
