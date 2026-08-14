import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getTicket } from '../api/tickets';
import { ApiError, toAbsoluteUrl } from '../api/client';
import type { Ticket } from '../types/ticket';

/**
 * One ticket, addressed by its unique id.
 *
 * Loads from the id in the URL rather than from anything the list passed along,
 * so the tracking link in the confirmation email works on a cold page load.
 * Stage 10 adds the admin edit controls.
 */
export default function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();

  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;

    const controller = new AbortController();

    setIsLoading(true);
    setError(null);

    getTicket(id, controller.signal)
      .then(setTicket)
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;

        setError(
          cause instanceof ApiError && cause.status === 404
            ? 'No ticket with that id. It may have been deleted.'
            : 'Could not load this ticket.',
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false);
      });

    return () => controller.abort();
  }, [id]);

  if (isLoading) return <main style={{ padding: 'var(--space-6)' }}>Loading…</main>;

  if (error || !ticket) {
    return (
      <main style={{ padding: 'var(--space-6)' }}>
        <p style={{ color: 'var(--danger)' }}>{error}</p>
        <Link to="/">Back to all tickets</Link>
      </main>
    );
  }

  return (
    <main style={{ padding: 'var(--space-6)', maxWidth: 720, margin: '0 auto' }}>
      <Link to="/">← All tickets</Link>

      <h1>{ticket.name}</h1>
      <p style={{ color: 'var(--text-muted)' }}>
        {ticket.email} · {ticket.status} · {ticket.id}
      </p>

      <h2>Issue</h2>
      <p>{ticket.description}</p>

      {ticket.summary && (
        <>
          <h2>Summary</h2>
          <p>{ticket.summary}</p>
        </>
      )}

      {ticket.imageUrl && (
        <>
          <h2>Attachment</h2>
          <img src={toAbsoluteUrl(ticket.imageUrl)} alt="Submitted by the customer" width={320} />
        </>
      )}

      {ticket.resolution && (
        <>
          <h2>Resolution</h2>
          <p>{ticket.resolution}</p>
        </>
      )}
    </main>
  );
}
