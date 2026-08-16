import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import AppShell from '../components/AppShell';
import Modal from '../components/Modal';
import NewTicketForm from '../components/NewTicketForm';
import Pager from '../components/Pager';
import StatusPill from '../components/StatusPill';
import { Banner, EmptyState, LoadingRows } from '../components/Feedback';
import { useTickets } from '../hooks/useTickets';
import { useDebounced } from '../hooks/useDebounced';
import { getStatuses } from '../api/tickets';
import {
  ANY_STATUS,
  DEFAULT_PAGE_SIZE,
  TICKET_STATUSES,
  type Ticket,
  type TicketStatus,
} from '../types/ticket';
import { formatDate } from '../utils/format';
import styles from './TicketsPage.module.css';
import ui from '../components/ui.module.css';

/**
 * The tickets list — the README's "Tickets View Screen".
 *
 * Read-only, with a click through to the detail page. The mockup shows status
 * and resolution editable inline, but the README's functional requirements put
 * editing on the detail screen ("Detailed Ticket View … edit status, add or
 * edit resolution text, save changes"), and one editing surface is easier to
 * keep correct than two.
 */
export default function TicketsPage() {
  const navigate = useNavigate();

  const [status, setStatus] = useState<string>(ANY_STATUS);
  const [searchInput, setSearchInput] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [confirmation, setConfirmation] = useState<Ticket | null>(null);

  // The raw value drives the input so typing stays instant; the debounced one
  // drives the request so the API is not queried per keystroke.
  const search = useDebounced(searchInput);

  const { tickets, page, isLoading, error, refresh } = useTickets({
    status,
    search,
    page: pageNumber,
    pageSize,
  });

  // Any change to what is being listed sends you back to the start. Staying on
  // page 4 while narrowing a search to three results would show an empty screen
  // and look like a failure rather than a filter.
  useEffect(() => {
    setPageNumber(1);
  }, [status, search, pageSize]);

  // Fetched rather than hardcoded, so the dropdown always offers exactly what
  // the server accepts. TICKET_STATUSES is the fallback if the call fails.
  const [statuses, setStatuses] = useState<readonly TicketStatus[]>(TICKET_STATUSES);

  useEffect(() => {
    const controller = new AbortController();
    getStatuses(controller.signal).then(setStatuses).catch(() => {});
    return () => controller.abort();
  }, []);

  function handleCreated(ticket: Ticket) {
    setIsModalOpen(false);
    setConfirmation(ticket);

    // The new ticket is the newest, so it is on page 1 — jump there rather than
    // refreshing a page it cannot appear on. On page 1 already, refresh anyway:
    // setting the same page number would not re-run the fetch.
    if (pageNumber === 1) {
      refresh();
    } else {
      setPageNumber(1);
    }
  }

  const isFiltered = status !== ANY_STATUS || search.trim().length > 0;

  // An empty page that is not an empty result: there are tickets, just not here.
  const isPastTheEnd = page.totalCount > 0 && pageNumber > 1;

  return (
    <AppShell>
      {confirmation && (
        <Banner tone="success">
          Ticket {confirmation.id.split('-')[0]} created. A confirmation email with a tracking link
          has been sent to {confirmation.email} — with the mock mail service it is printed to the
          API console.
        </Banner>
      )}

      <div className={styles.toolbar}>
        <div className={styles.filters}>
          <select
            className={`${ui.select} ${styles.statusFilter}`}
            value={status}
            onChange={(event) => setStatus(event.target.value)}
            aria-label="Filter by status"
          >
            <option value={ANY_STATUS}>All</option>
            {statuses.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>

          <input
            type="search"
            className={`${ui.input} ${styles.search}`}
            placeholder="Search name or description…"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            aria-label="Search tickets"
          />
        </div>

        <button
          type="button"
          className={`${ui.button} ${ui.primary}`}
          onClick={() => {
            setConfirmation(null);
            setIsModalOpen(true);
          }}
        >
          New Ticket
        </button>
      </div>

      {error && <Banner tone="error">{error}</Banner>}

      {/* Skeletons only when there is nothing to show yet. Replacing a full table
          with four placeholder rows on every page change collapses the page,
          which scrolls the pager out from under the cursor that just used it —
          so a page change dims the existing rows instead. */}
      {isLoading && tickets.length === 0 && <LoadingRows />}

      {!isLoading && !error && tickets.length === 0 && isPastTheEnd && (
        // Reachable if the result shrinks while you are deep in it — another
        // admin closing tickets you had filtered for, say. An empty page with no
        // way out reads as a broken screen, so offer the way out.
        <EmptyState title="This page is empty now">
          <button
            type="button"
            className={`${ui.button} ${ui.secondary}`}
            onClick={() => setPageNumber(1)}
          >
            Back to the first page
          </button>
        </EmptyState>
      )}

      {!isLoading && !error && tickets.length === 0 && !isPastTheEnd && (
        <EmptyState title={isFiltered ? 'No tickets match those filters' : 'No tickets yet'}>
          {isFiltered
            ? 'Try a different status, or clear the search box.'
            : 'When a customer files a ticket it will appear here.'}
        </EmptyState>
      )}

      {!error && tickets.length > 0 && (
        <>
          <div
            className={`${styles.tableWrap} ${isLoading ? styles.stale : ''}`}
            aria-busy={isLoading}
          >
            <table className={styles.table}>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Summary</th>
                  <th scope="col">Status</th>
                  <th scope="col">Resolution</th>
                  <th scope="col">Created</th>
                </tr>
              </thead>
              <tbody>
                {tickets.map((ticket) => (
                  <tr
                    key={ticket.id}
                    className={styles.row}
                    onClick={() => navigate(`/tickets/${ticket.id}`)}
                    // A clickable row is not reachable by keyboard on its own,
                    // so it is given a role, a tab stop, and Enter handling.
                    tabIndex={0}
                    role="link"
                    onKeyDown={(event) => {
                      if (event.key === 'Enter') navigate(`/tickets/${ticket.id}`);
                    }}
                  >
                    <td data-label="Name">
                      <span className={styles.customer}>{ticket.name}</span>
                      <span className={styles.email}>{ticket.email}</span>
                    </td>

                    <td data-label="Summary">
                      {ticket.summary ? (
                        <span className={styles.summary}>{ticket.summary}</span>
                      ) : (
                        <span className={styles.noSummary}>No summary</span>
                      )}
                    </td>

                    <td data-label="Status">
                      <StatusPill status={ticket.status} />
                    </td>

                    <td data-label="Resolution">
                      <span className={styles.resolution}>{ticket.resolution ?? '—'}</span>
                    </td>

                    <td data-label="Created">
                      <span className={styles.date}>{formatDate(ticket.createdAt)}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Pager
            page={page}
            onPageChange={setPageNumber}
            onPageSizeChange={setPageSize}
            noun={isFiltered ? 'matching tickets' : 'tickets'}
          />
        </>
      )}

      <Modal isOpen={isModalOpen} title="New Ticket" onClose={() => setIsModalOpen(false)}>
        <NewTicketForm onCreated={handleCreated} onCancel={() => setIsModalOpen(false)} />
      </Modal>
    </AppShell>
  );
}
