import { useContext } from 'react';
import { AuthContext, type AuthContextValue } from './context';

/** Access to the signed-in session. */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    // A clearer failure than the null-dereference the caller would otherwise hit.
    throw new Error('useAuth must be used inside an <AuthProvider>.');
  }

  return context;
}
