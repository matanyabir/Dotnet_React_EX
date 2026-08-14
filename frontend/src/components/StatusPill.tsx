import type { TicketStatus } from '../types/ticket';
import styles from './StatusPill.module.css';

/**
 * Keyed by the display string the API actually sends, so "In Progress" (with
 * its space) is the lookup key rather than something derived from it.
 */
const CLASS_BY_STATUS: Record<TicketStatus, string> = {
  New: styles.new,
  'In Progress': styles.inProgress,
  Resolved: styles.resolved,
  Closed: styles.closed,
};

/**
 * A ticket's status as a coloured pill.
 *
 * Carries a dot as well as a tint: colour alone is not an accessible way to
 * signal state, and the four statuses need to stay distinguishable to someone
 * who cannot separate the hues.
 */
export default function StatusPill({ status }: { status: TicketStatus }) {
  // An unrecognised status still renders, in neutral grey, rather than
  // disappearing — the frontend should not be the thing that hides new data.
  const tone = CLASS_BY_STATUS[status] ?? styles.closed;

  return <span className={`${styles.pill} ${tone}`}>{status}</span>;
}
