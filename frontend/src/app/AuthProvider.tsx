import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { logoutSession } from "../api/auth";
import { refreshAccessToken } from "../api/client";
import { accessTokenStore } from "../api/tokenStore";
import type { AuthSession } from "../types/auth";
import { sessionFromAccessToken } from "../utils/jwt";

interface AuthContextValue {
  session: AuthSession | null;
  isInitializing: boolean;
  establishSession: (accessToken: string) => void;
  refreshSession: () => Promise<boolean>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);

  const establishSession = useCallback((accessToken: string) => {
    accessTokenStore.set(accessToken);
    setSession(sessionFromAccessToken(accessToken));
  }, []);

  const refreshSession = useCallback(async () => {
    const accessToken = await refreshAccessToken();
    if (!accessToken) {
      setSession(null);
      return false;
    }

    establishSession(accessToken);
    return true;
  }, [establishSession]);

  useEffect(() => {
    void refreshSession().finally(() => {
      setIsInitializing(false);
    });
  }, [refreshSession]);

  const logout = useCallback(async () => {
    try {
      await logoutSession();
    } finally {
      accessTokenStore.clear();
      setSession(null);
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isInitializing,
      establishSession,
      refreshSession,
      logout,
    }),
    [establishSession, isInitializing, logout, refreshSession, session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.");
  }

  return context;
}
