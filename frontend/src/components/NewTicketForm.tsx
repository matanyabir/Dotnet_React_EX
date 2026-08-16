import { useRef, useState, type FormEvent } from 'react';
import { createTicket } from '../api/tickets';
import { ApiError } from '../api/client';
import type { Ticket } from '../types/ticket';
import { Banner } from './Feedback';
import styles from './ui.module.css';

interface NewTicketFormProps {
  onCreated: (ticket: Ticket) => void;
  onCancel: () => void;
}

/** Mirrors the server's rules so the obvious mistakes are caught before a round trip. */
function validate(name: string, email: string, description: string) {
  const errors: Record<string, string> = {};

  if (!name.trim()) errors.name = 'Please tell us your name.';
  if (!email.trim()) errors.email = 'Please give us an email address.';
  else if (!/^\S+@\S+\.\S+$/.test(email.trim())) errors.email = 'That does not look like an email address.';
  if (!description.trim()) errors.description = 'Please describe what went wrong.';

  return errors;
}

/**
 * The New Ticket form.
 *
 * Client-side validation here is a courtesy, not a guarantee — the server
 * validates the same fields and is the one that decides. Duplicating the cheap
 * checks just saves the customer a round trip to be told their email is blank.
 */
export default function NewTicketForm({ onCreated, onCancel }: NewTicketFormProps) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [description, setDescription] = useState('');
  const [image, setImage] = useState<File | null>(null);

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const found = validate(name, email, description);
    setErrors(found);
    setSubmitError(null);

    if (Object.keys(found).length > 0) return;

    setIsSubmitting(true);

    try {
      const ticket = await createTicket({ name, email, description }, image);

      // The modal keeps this form mounted, so state outlives a submit. Clear
      // what belongs to the ticket just filed — the next one is a different
      // problem — but leave name and email, which are the same person again.
      setDescription('');
      setImage(null);
      if (fileInputRef.current) fileInputRef.current.value = '';

      onCreated(ticket);
    } catch (cause: unknown) {
      // The server's message is more specific than anything guessable here —
      // an unsupported image format, for instance.
      setSubmitError(
        cause instanceof ApiError
          ? cause.messages.join(' ')
          : 'Could not reach the server. Please try again.',
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      {submitError && <Banner tone="error">{submitError}</Banner>}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="ticket-name">
          Name
        </label>
        <input
          id="ticket-name"
          className={`${styles.input} ${errors.name ? styles.invalid : ''}`}
          value={name}
          onChange={(event) => setName(event.target.value)}
          autoComplete="name"
          aria-invalid={Boolean(errors.name)}
          aria-describedby={errors.name ? 'ticket-name-error' : undefined}
        />
        {errors.name && (
          <span id="ticket-name-error" className={styles.fieldError}>
            {errors.name}
          </span>
        )}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="ticket-email">
          Email address
        </label>
        <input
          id="ticket-email"
          type="email"
          className={`${styles.input} ${errors.email ? styles.invalid : ''}`}
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          autoComplete="email"
          aria-invalid={Boolean(errors.email)}
          aria-describedby={errors.email ? 'ticket-email-error' : 'ticket-email-hint'}
        />
        {errors.email ? (
          <span id="ticket-email-error" className={styles.fieldError}>
            {errors.email}
          </span>
        ) : (
          <span id="ticket-email-hint" className={styles.hint}>
            We will send your tracking link here.
          </span>
        )}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="ticket-description">
          Description
        </label>
        <textarea
          id="ticket-description"
          className={`${styles.textarea} ${errors.description ? styles.invalid : ''}`}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          placeholder="What happened, and when did it start?"
          aria-invalid={Boolean(errors.description)}
          aria-describedby={errors.description ? 'ticket-description-error' : undefined}
        />
        {errors.description && (
          <span id="ticket-description-error" className={styles.fieldError}>
            {errors.description}
          </span>
        )}
      </div>

      <div className={styles.field}>
        <span className={styles.label}>
          Upload an image <span className={styles.optional}>(optional)</span>
        </span>

        <div className={styles.fileRow}>
          {/* The native file input is unstyleable across browsers, so it is
              hidden and driven by a button that matches the rest of the form. */}
          <input
            ref={fileInputRef}
            id="ticket-image"
            type="file"
            accept="image/png,image/jpeg,image/gif,image/webp"
            className={styles.srOnly}
            onChange={(event) => setImage(event.target.files?.[0] ?? null)}
          />

          <button
            type="button"
            className={`${styles.button} ${styles.secondary}`}
            onClick={() => fileInputRef.current?.click()}
          >
            Choose File
          </button>

          {image ? (
            <>
              <img className={styles.thumb} src={URL.createObjectURL(image)} alt="" />
              <span className={styles.fileName}>{image.name}</span>
              <button
                type="button"
                className={`${styles.button} ${styles.ghost}`}
                onClick={() => {
                  setImage(null);
                  // Clearing the input too, or re-picking the same file would
                  // not fire a change event.
                  if (fileInputRef.current) fileInputRef.current.value = '';
                }}
              >
                Remove
              </button>
            </>
          ) : (
            <span className={styles.fileName}>No file chosen</span>
          )}
        </div>
      </div>

      <div style={{ display: 'flex', gap: 'var(--space-3)', marginTop: 'var(--space-5)' }}>
        <button
          type="submit"
          className={`${styles.button} ${styles.accent} ${styles.block}`}
          disabled={isSubmitting}
        >
          {isSubmitting ? 'Submitting…' : 'Submit'}
        </button>
        <button
          type="button"
          className={`${styles.button} ${styles.secondary}`}
          onClick={onCancel}
          disabled={isSubmitting}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
