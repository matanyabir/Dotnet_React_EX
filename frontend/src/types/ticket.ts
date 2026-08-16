/**
 * The wire contract, mirroring the backend's DTOs.
 *
 * These are hand-written rather than generated: the API is small enough that a
 * generator would be more machinery than it saves, and writing them by hand
 * means a backend rename shows up here as a TypeScript error at build time
 * instead of as `undefined` in the browser.
 *
 * Field names are camelCase because the API serializes with web defaults.
 */

/**
 * Compile-time mirror of the backend's status vocabulary.
 *
 * `GET /api/tickets/statuses` is the *runtime* authority — the filter and edit
 * dropdowns fetch it so the UI cannot drift from the server. This constant
 * exists for type-safety and as the fallback if that request fails, which is
 * why `assertStatusesMatch` below checks the two agree.
 */
export const TICKET_STATUSES = ['New', 'In Progress', 'Closed', 'Resolved'] as const;

export type TicketStatus = (typeof TICKET_STATUSES)[number];

/** The filter dropdown's "no filter" option — the value the backend recognises. */
export const ANY_STATUS = 'All';

/**
 * One page of a list response, mirroring the backend's `PagedResult<T>`.
 *
 * `totalCount` is the size of the whole match rather than of `items`, which is
 * what lets the pager say "of 9" instead of only offering a "next".
 */
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** Rows per page. Mirrors the server's default; the request states it explicitly. */
export const DEFAULT_PAGE_SIZE = 20;

/** The sizes the list screen offers. All within the server's cap of 100. */
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export interface Ticket {
  id: string;
  name: string;
  email: string;
  description: string;
  /** AI-generated précis. Null when unavailable — always render conditionally. */
  summary: string | null;
  /** Storage-relative path such as `uploads/abc.png`, or null. */
  imageUrl: string | null;
  status: TicketStatus;
  resolution: string | null;
  /** ISO-8601. */
  createdAt: string;
  updatedAt: string;
}

/** What the New Ticket modal submits. The image travels separately as a file. */
export interface CreateTicketRequest {
  name: string;
  email: string;
  description: string;
}

/**
 * What the detail screen saves.
 *
 * Both fields are optional and mean different things when absent: omitting one
 * leaves it untouched, while sending `''` for `resolution` clears it. Sending
 * `undefined` and sending `''` are therefore not interchangeable.
 */
export interface UpdateTicketRequest {
  status?: TicketStatus;
  resolution?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  expiresAt: string;
  email: string;
  role: string;
}

/**
 * Warns in development if the server's status list has diverged from the
 * constant above — the drift that would otherwise show up as a filter that
 * silently matches nothing.
 */
export function assertStatusesMatch(fromServer: readonly string[]): void {
  if (!import.meta.env.DEV) return;

  const missing = fromServer.filter((s) => !TICKET_STATUSES.includes(s as TicketStatus));
  const extra = TICKET_STATUSES.filter((s) => !fromServer.includes(s));

  if (missing.length > 0 || extra.length > 0) {
    console.warn(
      'Status vocabulary drift between client and server.',
      { onlyOnServer: missing, onlyInClient: extra },
    );
  }
}
