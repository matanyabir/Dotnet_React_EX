import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTickets } from '../hooks/useTickets';
import { useDebounced } from '../hooks/useDebounced';
import { useAuth } from '../auth/useAuth';
import { ANY_STATUS } from '../types/ticket';

/**
 * The tickets list.
 *
 * Stage 9 wires the data path end to end — filters, debounce, loading and error
 * states — with plain markup. Stage 10 replaces the markup with the table,
 * status pills, and New Ticket modal from the mockup; the data flow below does
 * not change.
 */
export default function TicketsPage() {
  const [status, setStatus] = useState<string>(ANY_STATUS);
  const [searchInput, setSearchInput] = useState('');

  // The debounced value drives the request; the raw value drives the input, so
  // typing stays responsive while requests stay infrequent.
  const search = useDebounced(searchInput);

  const { tickets, isLoading, error } = useTickets({ status, search });
  const { session, isAdmin, signOut } = useAuth();

  return (
    <main style={{ padding: 'var(--space-6)', maxWidth: 960, margin: '0 auto' }}>
      <h1>Ticket Management</h1>

      <p style={{ color: 'var(--text-muted)' }}>
        {session
          ? `Signed in as ${session.email}${isAdmin ? ' (admin)' : ''}`
          : 'Not signed in — viewing only'}
        {session ? (
          <button type="button" onClick={signOut} style={{ marginLeft: 'var(--space-3)' }}>
            Sign out
          </button>
        ) : (
          <Link to="/login" style={{ marginLeft: 'var(--space-3)' }}>
            Sign in
          </Link>
        )}
      </p>

      <div style={{ display: 'flex', gap: 'var(--space-3)', margin: 'var(--space-5) 0' }}>
        <select value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value={ANY_STATUS}>All</option>
          <option value="New">New</option>
          <option value="In Progress">In Progress</option>
          <option value="Closed">Closed</option>
          <option value="Resolved">Resolved</option>
        </select>

        <input
          type="search"
          placeholder="Search…"
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
        />
      </div>

      {isLoading && <p>Loading tickets…</p>}
      {error && <p style={{ color: 'var(--danger)' }}>{error}</p>}

      {!isLoading && !error && tickets.length === 0 && <p>No tickets match those filters.</p>}

      <ul>
        {tickets.map((ticket) => (
          <li key={ticket.id}>
            <Link to={`/tickets/${ticket.id}`}>{ticket.name}</Link> — {ticket.status}
            {ticket.summary ? ` — ${ticket.summary}` : ''}
          </li>
        ))}
      </ul>
    </main>
  );
}
