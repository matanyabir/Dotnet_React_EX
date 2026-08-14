import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/useAuth';
import { ApiError } from '../api/client';

/**
 * Admin sign-in. Stage 10 gives it the card layout from login.png.
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
      // The server deliberately returns the same message for an unknown account
      // and a wrong password, so there is nothing to distinguish here either.
      setError(
        cause instanceof ApiError ? cause.message : 'Could not reach the server. Is the API running?',
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main style={{ padding: 'var(--space-6)', maxWidth: 420, margin: '0 auto' }}>
      <h1>Login</h1>

      <form onSubmit={handleSubmit}>
        <label htmlFor="email">Email address</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        {error && <p style={{ color: 'var(--danger)' }}>{error}</p>}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in…' : 'Login'}
        </button>
      </form>
    </main>
  );
}
