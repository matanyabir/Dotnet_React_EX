import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { login as requestLogin } from '../api/auth';
import { setAccessToken } from '../api/client';
import { AuthContext, SESSION_STORAGE_KEY, type AuthContextValue, type Session } from './context';

function hasExpired(session: Session): boolean {
  return new Date(session.expiresAt).getTime() <= Date.now();
}

/**
 * Reads a stored session, discarding anything unusable.
 *
 * The token is checked against its own expiry here rather than waiting for the
 * server to reject it: an expired token would otherwise leave the UI showing
 * edit controls that fail on save.
 */
function readStoredSession(): Session | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  if (!raw) return null;

  try {
    const session = JSON.parse(raw) as Session;

    if (!session.token || hasExpired(session)) {
      localStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }

    return session;
  } catch {
    // Corrupt or hand-edited storage: forget it rather than crash on boot.
    localStorage.removeItem(SESSION_STORAGE_KEY);
    return null;
  }
}

/**
 * Holds the signed-in session.
 *
 * Kept in localStorage so a page refresh does not sign the admin out mid-triage.
 * That is a deliberate trade: it survives refresh but is readable by any script
 * on the origin, which is acceptable for short-lived tokens guarding sample
 * data and would want revisiting for anything real.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  // Initialised lazily so storage is read once, before the first render, rather
  // than in an effect that would briefly render the app as signed out.
  const [session, setSession] = useState<Session | null>(() => {
    const stored = readStoredSession();
    setAccessToken(stored?.token ?? null);
    return stored;
  });

  // Keeps the API client's token in step with whatever the session is now.
  useEffect(() => {
    setAccessToken(session?.token ?? null);
  }, [session]);

  const signIn = useCallback(async (email: string, password: string) => {
    const response = await requestLogin({ email, password });

    const next: Session = {
      token: response.accessToken,
      email: response.email,
      role: response.role,
      expiresAt: response.expiresAt,
    };

    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(next));
    setSession(next);
  }, []);

  const signOut = useCallback(() => {
    localStorage.removeItem(SESSION_STORAGE_KEY);
    setSession(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAdmin: session?.role.toLowerCase() === 'admin',
      signIn,
      signOut,
    }),
    [session, signIn, signOut],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
