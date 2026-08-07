import { ApiError } from "../api/client";

interface ErrorStateProps {
  error: unknown;
}

export function ErrorState({ error }: ErrorStateProps) {
  const message =
    error instanceof ApiError || error instanceof Error
      ? error.message
      : "An unexpected error occurred while loading this information.";

  return (
    <p className="error-state" role="alert">
      {message}
    </p>
  );
}
