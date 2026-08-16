import { createContext } from 'react';

/**
 * What the UI knows about the signed-in admin.
 *
 * Note the absence of a token: the credential is an HttpOnly cookie the browser
 * holds and this code cannot read. Everything here is description — a name to
 * show in the header, a role to decide which controls to render — and none of it
 * grants anything. The server re-checks the cookie on every request regardless
 * of what this object claims.
 */
export interface Session {
  email: string;
  role: string;
  /** ISO-8601. Used to stop offering admin controls before the cookie lapses. */
  expiresAt: string;
}

export interface AuthContextValue {
  session: Session | null;
  /** True when the signed-in user may edit tickets. */
  isAdmin: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
}

/**
 * The context object itself, kept apart from the provider component.
 *
 * React Fast Refresh can only hot-reload a module that exports components and
 * nothing else — with the context in the same file, every edit to the provider
 * would drop the signed-in session.
 */
export const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Where the *description* of the session is cached — never the credential.
 *
 * It exists so a refresh renders the signed-in header immediately instead of
 * flashing the signed-out one while `/api/auth/me` answers. Tampering with it
 * buys nothing: it cannot produce a cookie, and the server is what decides.
 */
export const SESSION_STORAGE_KEY = 'mx.session';
