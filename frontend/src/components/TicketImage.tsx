import { useState } from 'react';
import { toAbsoluteUrl } from '../api/client';
import styles from './TicketImage.module.css';

/**
 * Renders a ticket's attachment, or an honest placeholder when the file is not
 * there.
 *
 * The supplied dataset references images (`uploads/laptop_issue.jpg` and
 * friends) that were never shipped with the exercise, so those rows always 404.
 * A bare <img> would show the browser's broken-image glyph and a stray alt
 * string, which reads like a bug in the app rather than missing sample data.
 */
export default function TicketImage({ path, alt }: { path: string; alt: string }) {
  const [failed, setFailed] = useState(false);

  if (failed) {
    return (
      <p className={styles.missing}>
        The attached image is no longer available.
        <span className={styles.missingPath}>{path}</span>
      </p>
    );
  }

  return (
    <a href={toAbsoluteUrl(path)} target="_blank" rel="noreferrer" className={styles.link}>
      <img
        src={toAbsoluteUrl(path)}
        alt={alt}
        className={styles.image}
        onError={() => setFailed(true)}
        // The image is decorative context, not the point of the page, so it
        // should never block the rest of it from painting.
        loading="lazy"
      />
    </a>
  );
}
