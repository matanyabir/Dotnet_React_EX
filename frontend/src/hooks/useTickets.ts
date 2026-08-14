import { useCallback, useEffect, useState } from 'react';
import { listTickets, type TicketFilters } from '../api/tickets';
import { ApiError } from '../api/client';
import type { Ticket } from '../types/ticket';

export interface UseTicketsResult {
  tickets: Ticket[];
  isLoading: boolean;
  error: string | null;
  /** Re-runs the current query — used after creating or editing a ticket. */
  refresh: () => void;
}

/**
 * Loads the ticket list for the given filters and keeps it in step with them.
 *
 * The fetch lives in a hook rather than a component so the screen stays about
 * layout, and so loading and error states are handled in one place instead of
 * being re-invented per view.
 */
export function useTickets(filters: TicketFilters): UseTicketsResult {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const refresh = useCallback(() => setReloadToken((n) => n + 1), []);

  // Destructured so the effect depends on the values, not on the identity of a
  // filters object that a parent may rebuild on every render.
  const { status, search } = filters;

  useEffect(() => {
    // Abort on change so a slow earlier request cannot overwrite the results of
    // a newer one — the classic out-of-order render when typing in a search box.
    const controller = new AbortController();

    setIsLoading(true);
    setError(null);

    listTickets({ status, search }, controller.signal)
      .then((result) => setTickets(result))
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;

        setError(
          cause instanceof ApiError
            ? cause.message
            : 'Could not reach the server. Is the API running?',
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false);
      });

    return () => controller.abort();
  }, [status, search, reloadToken]);

  return { tickets, isLoading, error, refresh };
}
