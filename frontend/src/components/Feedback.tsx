import type { ReactNode } from 'react';
import styles from './ui.module.css';

/**
 * The three states every data-driven screen needs. Sharing them keeps a failed
 * fetch looking the same wherever it happens, and stops each new screen from
 * inventing its own empty state.
 */

type BannerTone = 'error' | 'success' | 'info';

const TONE_CLASS: Record<BannerTone, string> = {
  error: styles.bannerError,
  success: styles.bannerSuccess,
  info: styles.bannerInfo,
};

export function Banner({ tone, children }: { tone: BannerTone; children: ReactNode }) {
  return (
    <p
      className={`${styles.banner} ${TONE_CLASS[tone]}`}
      // Errors interrupt; confirmations wait their turn. Both reach a screen
      // reader without the user having to go looking for them.
      role={tone === 'error' ? 'alert' : 'status'}
    >
      {children}
    </p>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className={styles.empty}>
      <p className={styles.emptyTitle}>{title}</p>
      {children}
    </div>
  );
}

/**
 * Placeholder rows shaped like the content they stand in for, so the page does
 * not reflow when the real rows land.
 */
export function LoadingRows({ count = 4 }: { count?: number }) {
  return (
    <div aria-busy="true" aria-live="polite">
      <span className={styles.srOnly}>Loading tickets…</span>
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className={styles.skeletonRow} />
      ))}
    </div>
  );
}
