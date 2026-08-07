import { environment } from "../utils/environment";
import type { RefreshSessionResponse } from "../types/auth";
import { accessTokenStore } from "./tokenStore";

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  currentResource?: {
    id: string;
    rowVersion: string;
  };
  issues?: Array<{
    code: string;
    message: string;
    productOptionGroupId?: string;
  }>;
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status?: number,
    readonly code?: string,
    readonly traceId?: string,
    readonly errors?: Record<string, string[]>,
    readonly problemDetails?: ProblemDetails,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

interface ApiRequestOptions extends RequestInit {
  retryUnauthorized?: boolean;
}

const apiBaseUrl = new URL(environment.apiUrl, window.location.origin);
let refreshPromise: Promise<string | null> | null = null;

function createApiUrl(path: string): URL {
  const requestPath = path.replace(/^\/+/, "");
  const baseUrl = apiBaseUrl.toString().replace(/\/+$/, "");
  return new URL(`${baseUrl}/${requestPath}`);
}

function createRootUrl(path: string): URL {
  return new URL(path.startsWith("/") ? path : `/${path}`, apiBaseUrl.origin);
}

function readCookie(name: string): string | undefined {
  const prefix = `${encodeURIComponent(name)}=`;
  const cookie = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));

  return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : undefined;
}

async function parseProblemDetails(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}

function buildHeaders(init: RequestInit): Headers {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  const accessToken = accessTokenStore.get();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  return headers;
}

async function fetchApi(
  url: URL,
  init: RequestInit,
): Promise<Response> {
  try {
    return await fetch(url, {
      ...init,
      credentials: "include",
      headers: buildHeaders(init),
    });
  } catch {
    throw new ApiError(
      "The backend could not be reached. Check that it is running and the frontend environment URLs are correct.",
    );
  }
}

export async function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    const csrfToken = readCookie(environment.csrfCookieName);
    if (!csrfToken) {
      accessTokenStore.clear();
      return null;
    }

    const response = await fetchApi(createApiUrl("auth/refresh"), {
      method: "POST",
      headers: {
        [environment.csrfHeaderName]: csrfToken,
      },
    });

    if (!response.ok) {
      accessTokenStore.clear();
      return null;
    }

    const payload = (await response.json()) as RefreshSessionResponse;
    accessTokenStore.set(payload.accessToken);
    return payload.accessToken;
  })().finally(() => {
    refreshPromise = null;
  });

  return refreshPromise;
}

async function requestJson<T>(
  url: URL,
  options: ApiRequestOptions = {},
): Promise<T> {
  const { retryUnauthorized = true, ...init } = options;
  let response = await fetchApi(url, init);

  if (response.status === 401 && retryUnauthorized) {
    const refreshedToken = await refreshAccessToken();
    if (refreshedToken) {
      response = await fetchApi(url, init);
    }
  }

  if (!response.ok) {
    const problemDetails = await parseProblemDetails(response);
    throw new ApiError(
      problemDetails?.detail ??
        problemDetails?.title ??
        `The request failed with status ${response.status}.`,
      response.status,
      problemDetails?.code,
      problemDetails?.traceId,
      problemDetails?.errors,
      problemDetails,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function createJsonRequest(
  method: "POST" | "PATCH" | "PUT" | "DELETE",
  body: unknown,
  options: ApiRequestOptions = {},
): ApiRequestOptions {
  const headers = new Headers(options.headers);
  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  return {
    ...options,
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  };
}

export const apiClient = {
  get<T>(path: string, options?: ApiRequestOptions): Promise<T> {
    return requestJson<T>(createApiUrl(path), options);
  },
  getRoot<T>(path: string, options?: ApiRequestOptions): Promise<T> {
    return requestJson<T>(createRootUrl(path), options);
  },
  post<T>(
    path: string,
    body?: unknown,
    options?: ApiRequestOptions,
  ): Promise<T> {
    return requestJson<T>(
      createApiUrl(path),
      createJsonRequest("POST", body, options),
    );
  },
  patch<T>(
    path: string,
    body?: unknown,
    options?: ApiRequestOptions,
  ): Promise<T> {
    return requestJson<T>(
      createApiUrl(path),
      createJsonRequest("PATCH", body, options),
    );
  },
  put<T>(
    path: string,
    body?: unknown,
    options?: ApiRequestOptions,
  ): Promise<T> {
    return requestJson<T>(
      createApiUrl(path),
      createJsonRequest("PUT", body, options),
    );
  },
  delete<T>(
    path: string,
    body?: unknown,
    options?: ApiRequestOptions,
  ): Promise<T> {
    return requestJson<T>(
      createApiUrl(path),
      createJsonRequest("DELETE", body, options),
    );
  },
  upload<T>(
    path: string,
    formData: FormData,
    options: ApiRequestOptions = {},
  ): Promise<T> {
    return requestJson<T>(createApiUrl(path), {
      ...options,
      method: "POST",
      body: formData,
    });
  },
};
