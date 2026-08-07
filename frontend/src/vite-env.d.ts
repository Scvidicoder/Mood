/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_SIGNALR_URL: string;
  readonly VITE_CSRF_COOKIE_NAME?: string;
  readonly VITE_CSRF_HEADER_NAME?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
