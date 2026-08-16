import { request } from './client';
import {
  ANY_STATUS,
  DEFAULT_PAGE_SIZE,
  TICKET_STATUSES,
  assertStatusesMatch,
  type CreateTicketRequest,
  type Page,
  type Ticket,
  type TicketStatus,
  type UpdateTicketRequest,
} from '../types/ticket';

export interface TicketFilters {
  status?: string;
  search?: string;
  /** 1-based. Omitted means the first page. */
  page?: number;
  pageSize?: number;
}

/**
 * Builds the query string, omitting filters that are not active.
 *
 * "All" and blank both mean "no filter"; sending them anyway would work, but
 * leaving them off keeps the URL readable and the request cacheable.
 *
 * Paging is the exception: `page` and `pageSize` are always sent, because a
 * client that relies on the server's default silently changes behaviour the day
 * that default moves — and the pager's arithmetic depends on knowing the size.
 */
function toQuery({ status, search, page, pageSize }: TicketFilters): string {
  const params = new URLSearchParams();

  if (status && status !== ANY_STATUS) params.set('status', status);
  if (search?.trim()) params.set('search', search.trim());

  params.set('page', String(page ?? 1));
  params.set('pageSize', String(pageSize ?? DEFAULT_PAGE_SIZE));

  return `?${params.toString()}`;
}

export function listTickets(
  filters: TicketFilters = {},
  signal?: AbortSignal,
): Promise<Page<Ticket>> {
  return request<Page<Ticket>>(`/api/tickets${toQuery(filters)}`, { signal });
}

export function getTicket(id: string, signal?: AbortSignal): Promise<Ticket> {
  return request<Ticket>(`/api/tickets/${id}`, { signal });
}

/**
 * Files a ticket. Always multipart, whether or not an image was chosen — one
 * code path is easier to reason about than two, and the API accepts it either way.
 */
export function createTicket(ticket: CreateTicketRequest, image?: File | null): Promise<Ticket> {
  const form = new FormData();
  form.set('name', ticket.name);
  form.set('email', ticket.email);
  form.set('description', ticket.description);

  if (image) form.set('image', image);

  return request<Ticket>('/api/tickets', { method: 'POST', form });
}

/** Admin-only. Omitted fields are left untouched by the server. */
export function updateTicket(id: string, changes: UpdateTicketRequest): Promise<Ticket> {
  return request<Ticket>(`/api/tickets/${id}`, { method: 'PUT', body: changes });
}

/**
 * The status vocabulary, from the server.
 *
 * Fetched rather than hardcoded so the dropdowns cannot drift from the backend —
 * the exact drift that would break "In Progress". Falls back to the local
 * constant if the request fails, so a filter dropdown is never empty.
 */
export async function getStatuses(signal?: AbortSignal): Promise<readonly TicketStatus[]> {
  try {
    const statuses = await request<string[]>('/api/tickets/statuses', { signal });
    assertStatusesMatch(statuses);
    return statuses as TicketStatus[];
  } catch {
    return TICKET_STATUSES;
  }
}
