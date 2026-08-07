function requireEnvironmentValue(
  key: "VITE_API_URL" | "VITE_SIGNALR_URL",
  value: string | undefined,
): string {
  const normalizedValue = value?.trim();

  if (!normalizedValue) {
    throw new Error(`${key} must be configured.`);
  }

  return normalizedValue;
}

export const environment = Object.freeze({
  apiUrl: requireEnvironmentValue("VITE_API_URL", import.meta.env.VITE_API_URL),
  signalRUrl: requireEnvironmentValue(
    "VITE_SIGNALR_URL",
    import.meta.env.VITE_SIGNALR_URL,
  ),
  csrfCookieName:
    import.meta.env.VITE_CSRF_COOKIE_NAME?.trim() ||
    "__Secure-MoodPickup.Csrf",
  csrfHeaderName:
    import.meta.env.VITE_CSRF_HEADER_NAME?.trim() || "X-CSRF-TOKEN",
});
