export function queryString(
  values: Record<string, string | number | boolean | null | undefined>,
): string {
  const parameters = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      parameters.set(key, String(value));
    }
  });
  const result = parameters.toString();
  return result ? `?${result}` : "";
}
