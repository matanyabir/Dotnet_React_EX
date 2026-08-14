import { useEffect, useState, type FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import AppShell from '../components/AppShell';
import StatusPill from '../components/StatusPill';
import TicketImage from '../components/TicketImage';
import { Banner, LoadingRows } from '../components/Feedback';
import { getStatuses, getTicket, updateTicket } from '../api/tickets';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { TICKET_STATUSES, type Ticket, type TicketStatus, type UpdateTicketRequest } from '../types/ticket';
import { formatDateTime } from '../utils/format';
import styles from './TicketDetailPage.module.css';
import ui from '../components/ui.module.css';

/**
 * One ticket, addressed by its unique id — the README's "Detailed Ticket View
 * Screen (Accessible by Unique ID)".
 *
 * Everything is loaded from the id in the URL, never handed over by the list,
 * so the tracking link in the confirmation email works on a cold page load.
 */
export default function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { isAdmin } = useAuth();

  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // The edit form's working copy, kept apart from the loaded ticket so the
  // page can show what was saved even while unsaved edits sit in the fields.
  const [statusDraft, setStatusDraft] = useState<TicketStatus>('New');
  const [resolutionDraft, setResolutionDraft] = useState('');
  const [statuses, setStatuses] = useState<readonly TicketStatus[]>(TICKET_STATUSES);

  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveNotice, setSaveNotice] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getStatuses(controller.signal).then(setStatuses).catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!id) return;

    const controller = new AbortController();

    setIsLoading(true);
    setLoadError(null);

    getTicket(id, controller.signal)
      .then((loaded) => {
        setTicket(loaded);
        setStatusDraft(loaded.status);
        setResolutionDraft(loaded.resolution ?? '');
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return;

        setLoadError(
          cause instanceof ApiError && cause.status === 404
            ? 'No ticket with that reference. Check the link, or browse all tickets.'
            : 'Could not load this ticket. Is the API running?',
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false);
      });

    return () => controller.abort();
  }, [id]);

  const hasChanges =
    ticket !== null &&
    (statusDraft !== ticket.status || resolutionDraft !== (ticket.resolution ?? ''));

  async function handleSave(event: FormEvent) {
    event.preventDefault();

    if (!ticket) return;

    setIsSaving(true);
    setSaveError(null);
    setSaveNotice(null);

    // Only changed fields are sent. Omitting a field tells the server to leave
    // it alone, which is also what stops an unchanged save from emailing the
    // customer about nothing.
    const changes: UpdateTicketRequest = {};
    if (statusDraft !== ticket.status) changes.status = statusDraft;
    if (resolutionDraft !== (ticket.resolution ?? '')) changes.resolution = resolutionDraft;

    try {
      const updated = await updateTicket(ticket.id, changes);

      setTicket(updated);
      setStatusDraft(updated.status);
      setResolutionDraft(updated.resolution ?? '');
      setSaveNotice('Saved. The customer has been emailed about the change.');
    } catch (cause: unknown) {
      if (cause instanceof ApiError && (cause.isUnauthorized || cause.isForbidden)) {
        setSaveError('Your session has expired or lacks permission. Sign in again to save.');
      } else {
        setSaveError(
          cause instanceof ApiError ? cause.messages.join(' ') : 'Could not save. Please try again.',
        );
      }
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return (
      <AppShell>
        <LoadingRows count={3} />
      </AppShell>
    );
  }

  if (loadError || !ticket) {
    return (
      <AppShell>
        <Banner tone="error">{loadError}</Banner>
        <Link to="/" className={`${ui.button} ${ui.secondary}`}>
          Back to all tickets
        </Link>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <Link to="/" className={styles.back}>
        ← All tickets
      </Link>

      <div className={styles.layout}>
        <article className={`${ui.card} ${styles.panel}`}>
          <div className={styles.heading}>
            <h1 className={styles.name}>{ticket.name}</h1>
            <StatusPill status={ticket.status} />
          </div>

          <p className={styles.meta}>
            {ticket.email} · opened {formatDateTime(ticket.createdAt)}
            {ticket.updatedAt !== ticket.createdAt && ` · updated ${formatDateTime(ticket.updatedAt)}`}
            <br />
            <span className={styles.reference}>Reference: {ticket.id}</span>
          </p>

          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Issue description</h2>
            <p className={styles.description}>{ticket.description}</p>
          </section>

          {ticket.summary && (
            <section className={styles.section}>
              <h2 className={styles.sectionTitle}>AI summary</h2>
              <p className={styles.summary}>{ticket.summary}</p>
            </section>
          )}

          {ticket.imageUrl && (
            <section className={styles.section}>
              <h2 className={styles.sectionTitle}>Attachment</h2>
              <TicketImage path={ticket.imageUrl} alt={`Submitted with ${ticket.name}'s ticket`} />
            </section>
          )}
        </article>

        <aside className={`${ui.card} ${styles.panel}`}>
          <h2 className={styles.sectionTitle}>{isAdmin ? 'Update ticket' : 'Status and resolution'}</h2>

          {saveError && <Banner tone="error">{saveError}</Banner>}
          {saveNotice && <Banner tone="success">{saveNotice}</Banner>}

          {isAdmin ? (
            <form onSubmit={handleSave}>
              <div className={ui.field}>
                <label className={ui.label} htmlFor="ticket-status">
                  Status
                </label>
                <select
                  id="ticket-status"
                  className={ui.select}
                  value={statusDraft}
                  onChange={(event) => setStatusDraft(event.target.value as TicketStatus)}
                  disabled={isSaving}
                >
                  {statuses.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>

              <div className={ui.field}>
                <label className={ui.label} htmlFor="ticket-resolution">
                  Resolution
                </label>
                <textarea
                  id="ticket-resolution"
                  className={ui.textarea}
                  value={resolutionDraft}
                  onChange={(event) => setResolutionDraft(event.target.value)}
                  placeholder="What was done about it?"
                  disabled={isSaving}
                />
                <span className={ui.hint}>
                  The customer is emailed whenever this or the status changes.
                </span>
              </div>

              <div className={styles.actions}>
                <button
                  type="submit"
                  className={`${ui.button} ${ui.primary}`}
                  // Nothing to save is not an error worth explaining after the
                  // fact — the button simply stays inert until something changes.
                  disabled={isSaving || !hasChanges}
                >
                  {isSaving ? 'Saving…' : 'Save changes'}
                </button>

                {hasChanges && !isSaving && (
                  <button
                    type="button"
                    className={`${ui.button} ${ui.ghost}`}
                    onClick={() => {
                      setStatusDraft(ticket.status);
                      setResolutionDraft(ticket.resolution ?? '');
                      setSaveNotice(null);
                    }}
                  >
                    Discard
                  </button>
                )}
              </div>
            </form>
          ) : (
            <>
              <div className={styles.section}>
                <h3 className={styles.sectionTitle}>Status</h3>
                <StatusPill status={ticket.status} />
              </div>

              <div className={styles.section}>
                <h3 className={styles.sectionTitle}>Resolution</h3>
                {ticket.resolution ? (
                  <p className={styles.readOnlyValue}>{ticket.resolution}</p>
                ) : (
                  <p className={styles.muted}>Nothing recorded yet.</p>
                )}
              </div>

              <p className={styles.signInPrompt}>
                <Link to="/login">Sign in as an admin</Link> to update this ticket.
              </p>
            </>
          )}
        </aside>
      </div>
    </AppShell>
  );
}
