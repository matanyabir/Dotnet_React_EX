/** Shared formatting helpers, so dates read the same on every screen. */

/** e.g. "27 Oct 2025, 14:35" — unambiguous without being long. */
export function formatDateTime(iso: string): string {
  const date = new Date(iso);

  if (Number.isNaN(date.getTime())) return '—';

  return date.toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** e.g. "27 Oct 2025" — for table cells, where the time is noise. */
export function formatDate(iso: string): string {
  const date = new Date(iso);

  if (Number.isNaN(date.getTime())) return '—';

  return date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}

/** First segment of a ticket id — enough to recognise, short enough to show. */
export function shortId(id: string): string {
  return id.split('-')[0] ?? id;
}
