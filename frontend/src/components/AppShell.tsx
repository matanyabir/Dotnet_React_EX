import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import styles from './AppShell.module.css';

/**
 * Page chrome shared by every screen: the brand bar and the sign-in state.
 *
 * Having it in one place means the header cannot drift between screens, and
 * "am I signed in?" is answerable from anywhere in the app without hunting.
 */
export default function AppShell({ children }: { children: ReactNode }) {
  const { session, isAdmin, signOut } = useAuth();

  return (
    <>
      <header className={styles.header}>
        <div className={styles.headerInner}>
          <Link to="/" className={styles.brand}>
            <span className={styles.title}>Ticket Management</span>
            <span className={styles.tagline}>Customer support</span>
          </Link>

          <div className={styles.session}>
            {session ? (
              <>
                <span className={styles.who}>
                  {session.email}
                  {isAdmin ? ' · admin' : ''}
                </span>
                <button
                  type="button"
                  className={styles.headerButton}
                  // Signing out is a round-trip now — the cookie is the server's
                  // to delete — and a click handler has nowhere to put a promise.
                  onClick={() => void signOut()}
                >
                  Sign out
                </button>
              </>
            ) : (
              <Link to="/login" className={styles.headerButton}>
                Admin sign in
              </Link>
            )}
          </div>
        </div>
      </header>

      <main className={styles.main}>{children}</main>
    </>
  );
}
