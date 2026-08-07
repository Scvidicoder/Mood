export type AccountType = "customer" | "employee";

export interface RequestCodeResponse {
  challengeId: string;
  expiresInSeconds: number;
  resendAvailableInSeconds: number;
  telegramBotUrl: string;
  clientChallengeSecret: string;
  status: CustomerChallengeStatus;
}

export type CustomerChallengeStatus =
  | "WaitingForTelegramStart"
  | "WaitingForTelegramContact"
  | "OtpSent"
  | "Expired"
  | "Locked"
  | "Completed";

export interface CustomerChallengeStatusResponse {
  status: CustomerChallengeStatus;
  expiresInSeconds: number;
  canResend: boolean;
}

export interface TelegramLoginRouteState {
  challengeId: string;
  clientChallengeSecret: string;
  phoneNumber: string;
  telegramBotUrl: string;
  expiresInSeconds: number;
  resendAvailableInSeconds: number;
}

export interface CustomerSummary {
  id: string;
  name: string;
  phoneNumber: string;
}

export interface CustomerVerificationResponse {
  isNewCustomer: boolean;
  accessToken?: string;
  expiresInSeconds?: number;
  customer?: CustomerSummary;
  registrationToken?: string;
}

export interface CustomerAuthenticationResponse {
  accessToken: string;
  expiresInSeconds: number;
  customer: CustomerSummary;
}

export interface EmployeeSummary {
  id: string;
  fullName: string;
  username: string;
  roles: string[];
}

export interface EmployeeAuthenticationResponse {
  accessToken: string;
  expiresInSeconds: number;
  mustChangePassword: boolean;
  employee: EmployeeSummary;
}

export interface RefreshSessionResponse {
  accessToken: string;
  expiresInSeconds: number;
}

export interface AuthSession {
  accountId: string;
  accountType: AccountType;
  phoneNumber?: string;
  username?: string;
  fullName?: string;
  roles: string[];
  mustChangePassword: boolean;
}
