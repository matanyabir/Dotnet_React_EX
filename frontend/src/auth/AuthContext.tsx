import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { currentSession, login as requestLogin, logout as requestLogout } from '../api/auth';
import { ApiError } from '../api/client';
import { AuthContext, SESSION_STORAGE_KEY, type AuthContextValue, type Session } from './context';

function hasExpired(session: Session): boolean {
  return new Date(session.expiresAt).getTime() <= Date.now();
}

function remember(session: Session): void {
  localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

function forget(): void {
  localStorage.removeItem(SESSION_STORAGE_KEY);
}

/**
 * Reads the cached session description, discarding anything unusable.
 *
 * Expiry is checked here rather than waiting for the server to reject: a lapsed
 * session would otherwise leave the UI showing edit controls that fail on save.
 */
function readStoredSession(): Session | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  if (!raw) return null;

  try {
    const session = JSON.parse(raw) as Session;

    if (!session.email || hasExpired(session)) {
      forget();
      return null;
    }

    return session;
  } catch {
    // Corrupt or hand-edited storage: forget it rather than crash on boot.
    forget();
    return null;
  }
}

/**
 * Holds the signed-in session.
 *
 * The token lives in an HttpOnly cookie, so this provider never sees it and no
 * script on the origin can read it — an injected script cannot lift the admin's
 * session the way it could when the token sat in localStorage. What stays in
 * storage is only the session's description, cached so a refresh does not flash
 * the signed-out header on its way to rendering the signed-in one.
 *
 * Because that cache is a guess, it is reconciled against the server on mount:
 * whoever the cookie actually belongs to wins.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  // Initialised lazily so storage is read once, before the first render, rather
  // than in an effect that would briefly render the app as signed out.
  const [session, setSession] = useState<Session | null>(readStoredSession);

  useEffect(() => {
    let cancelled = false;

    // A 401 here is the ordinary signed-out answer, not a failure worth
    // surfacing. Any other error (the API being down) leaves the cached
    // description alone: the next real request will fail visibly on its own,
    // and signing the admin out over a blip would be its own bug.
    currentSession().then(
      (server) => {
        if (cancelled) return;

        const next: Session = {
          email: server.email,
          role: server.role,
          expiresAt: server.expiresAt,
        };

        remember(next);
        setSession(next);
      },
      (cause: unknown) => {
        if (cancelled) return;

        if (cause instanceof ApiError && cause.isUnauthorized) {
          forget();
          setSession(null);
        }
      },
    );

    return () => {
      cancelled = true;
    };
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    // The response carries no token — the cookie the browser just stored does.
    const response = await requestLogin({ email, password });

    const next: Session = {
      email: response.email,
      role: response.role,
      expiresAt: response.expiresAt,
    };

    remember(next);
    setSession(next);
  }, []);

  const signOut = useCallback(async () => {
    // Clearing local state is not enough now: only the server can delete a
    // cookie the page is not allowed to touch.
    try {
      await requestLogout();
    } catch {
      // The API is unreachable. The tab is still signed out below, which is what
      // the click asked for — but the cookie outlives the request, so a refresh
      // against a recovered API will restore the session. Nothing here can
      // prevent that, and pretending otherwise would be the worse failure.
    }

    forget();
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
