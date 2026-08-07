import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    globals: true,
    css: true,
    environmentOptions: {
      jsdom: {
        url: "https://localhost",
      },
    },
    env: {
      VITE_API_URL: "https://api.test/api/v1",
      VITE_SIGNALR_URL: "https://api.test/hubs/notifications",
      VITE_CSRF_COOKIE_NAME: "__Secure-MoodPickup.Csrf",
      VITE_CSRF_HEADER_NAME: "X-CSRF-TOKEN",
    },
  },
  server: {
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
  },
  preview: {
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
  },
});
