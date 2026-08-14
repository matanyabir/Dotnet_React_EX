import { useEffect, useState } from 'react';

/**
 * Delays a rapidly-changing value.
 *
 * Used for the search box: without it every keystroke is a request, and the
 * responses can land out of order so the list briefly shows results for a
 * prefix of what was typed.
 */
export function useDebounced<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);

    // Cancels the pending update whenever the value changes again, which is
    // what makes this debounce rather than throttle.
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
