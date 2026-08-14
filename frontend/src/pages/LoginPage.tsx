import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { ApiError } from '../api/client';
import { Banner } from '../components/Feedback';
import styles from './LoginPage.module.css';
import ui from '../components/ui.module.css';

/**
 * Admin sign-in, laid out to match login.png.
 *
 * The mockup shows a "Need an account? Register" link. The README only asks for
 * login — accounts are configured server-side — so that line explains where
 * accounts come from rather than linking to a registration flow that does not
 * exist. A dead link would be worse than none.
 */
export default function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    setIsSubmitting(true);
    setError(null);

    try {
      await signIn(email, password);
      navigate('/');
    } catch (cause: unknown) {
      // The server returns the same message for an unknown account and a wrong
      // password on purpose, so there is nothing to distinguish here either —
      // doing so would undo that protection from the client side.
      setError(
        cause instanceof ApiError
          ? cause.messages.join(' ')
          : 'Could not reach the server. Is the API running?',
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={`${ui.card} ${styles.card}`}>
        <h1 className={styles.title}>Login</h1>

        {error && <Banner tone="error">{error}</Banner>}

        <form onSubmit={handleSubmit}>
          <div className={ui.field}>
            <label className={ui.label} htmlFor="login-email">
              Email address
            </label>
            <input
              id="login-email"
              type="email"
              className={ui.input}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="username"
              required
              autoFocus
            />
          </div>

          <div className={ui.field}>
            <label className={ui.label} htmlFor="login-password">
              Password
            </label>
            <input
              id="login-password"
              type="password"
              className={ui.input}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          <button
            type="submit"
            className={`${ui.button} ${ui.accent} ${ui.block}`}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Signing in…' : 'Login'}
          </button>
        </form>

        <p className={styles.footer}>Admin accounts are configured on the server.</p>

        <Link to="/" className={styles.back}>
          ← Back to tickets
        </Link>

        {import.meta.env.DEV && (
          <p className={styles.devHint}>
            Development credentials: <code>admin@example.com</code> / <code>Admin123!</code>
          </p>
        )}
      </div>
    </div>
  );
}
