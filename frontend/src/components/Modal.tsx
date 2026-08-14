import { useEffect, useRef, type ReactNode } from 'react';
import styles from './Modal.module.css';

interface ModalProps {
  isOpen: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}

/**
 * A modal dialog, built on the native <dialog> element.
 *
 * `showModal()` gives focus trapping, Escape-to-close, the backdrop, and
 * `inert` on the rest of the page for free — all things a hand-rolled div
 * modal has to reimplement and usually gets subtly wrong. The only work left
 * is keeping the element's open state in step with React's.
 */
export default function Modal({ isOpen, title, onClose, children }: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (isOpen && !dialog.open) {
      dialog.showModal();
    } else if (!isOpen && dialog.open) {
      dialog.close();
    }
  }, [isOpen]);

  return (
    <dialog
      ref={dialogRef}
      className={styles.dialog}
      aria-labelledby="modal-title"
      // Fires when the browser closes the dialog itself — Escape, mostly — so
      // React's state follows the DOM rather than drifting out of sync with it.
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClose={onClose}
      // Clicking the backdrop closes. The check compares against the dialog
      // itself because the backdrop is not a separate element to listen on.
      onClick={(event) => {
        if (event.target === dialogRef.current) onClose();
      }}
    >
      <div className={styles.panel}>
        <header className={styles.header}>
          <h2 id="modal-title" className={styles.title}>
            {title}
          </h2>
          <button type="button" className={styles.close} onClick={onClose} aria-label="Close">
            ×
          </button>
        </header>

        <div className={styles.body}>{children}</div>
      </div>
    </dialog>
  );
}
