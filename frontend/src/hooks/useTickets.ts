import { useCallback, useEffect, useState } from 'react';
import { listTickets, type TicketFilters } from '../api/tickets';
import { ApiError } from '../api/client';
import { DEFAULT_PAGE_SIZE, type Page, type Ticket } from '../types/ticket';

export interface UseTicketsResult {
  /** The rows on the current page. */
  tickets: Ticket[];
  /** Everything the pager needs: totals, bounds, and which page this is. */
  page: Page<Ticket>;
  isLoading: boolean;
  error: string | null;
  /** Re-runs the current query — used after creating or editing a ticket. */
  refresh: () => void;
}

/** Shown before the first response lands, so the pager never reads `undefined`. */
function emptyPage(pageNumber: number, pageSize: number): Page<Ticket> {
  return {
    items: [],
    page: pageNumber,
    pageSize,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}

/**
 * Loads a page of tickets for the given filters and keeps it in step with them.
 *
 * The fetch lives in a hook rather than a component so the screen stays about
 * layout, and so loading and error states are handled in one place instead of
 * being re-invented per view.
 *
 * The page number is an input, not internal state: which page you are on is the
 * screen's concern — it has to be reset when a filter changes — while fetching
 * that page is this hook's.
 */
export function useTickets(filters: TicketFilters): UseTicketsResult {
  // Destructured so the effect depends on the values, not on the identity of a
  // filters object that a parent may rebuild on every render.
  const { status, search, page: pageNumber = 1, pageSize = DEFAULT_PAGE_SIZE } = filters;

  const [page, setPage] = useState<Page<Ticket>>(() => emptyPage(pageNumber, pageSize));
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const refresh = useCallback(() => setReloadToken((n) => n + 1), []);

  useEffect(() => {
    // Abort on change so a slow earlier request cannot overwrite the results of
    // a newer one — the classic out-of-order render when typing in a search box,
    // and equally when clicking through pages faster than they load.
    const controller = new AbortController();

    setIsLoading(true);
    setError(null);

    listTickets({ status, search, page: pageNumber, pageSize }, controller.signal)
      .then((result) => setPage(result))
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;

        // Drop the stale rows: leaving the previous page on screen under an error
        // banner would show data that no longer answers the current query.
        setPage(emptyPage(pageNumber, pageSize));

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
  }, [status, search, pageNumber, pageSize, reloadToken]);

  return { tickets: page.items, page, isLoading, error, refresh };
}
