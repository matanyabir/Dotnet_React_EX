import { createContext } from 'react';

export interface Session {
  token: string;
  email: string;
  role: string;
  /** ISO-8601. Used to drop a stale token before it is sent. */
  expiresAt: string;
}

export interface AuthContextValue {
  session: Session | null;
  /** True when the signed-in user may edit tickets. */
  isAdmin: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => void;
}

/**
 * The context object itself, kept apart from the provider component.
 *
 * React Fast Refresh can only hot-reload a module that exports components and
 * nothing else — with the context in the same file, every edit to the provider
 * would drop the signed-in session.
 */
export const AuthContext = createContext<AuthContextValue | null>(null);

export const SESSION_STORAGE_KEY = 'mx.session';
