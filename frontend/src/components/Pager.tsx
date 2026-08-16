import { PAGE_SIZE_OPTIONS, type Page } from '../types/ticket';
import styles from './Pager.module.css';
import ui from './ui.module.css';

interface PagerProps<T> {
  page: Page<T>;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  /** Plural noun for the running total, e.g. "tickets". */
  noun?: string;
}

/**
 * Page controls for a list.
 *
 * First and Last are here alongside Prev and Next because a dataset large enough
 * to need paging is one where "the oldest ticket" is otherwise thirty clicks
 * away. There are no numbered page buttons: with an unknown number of pages they
 * either wrap awkwardly or need elision logic, and neither earns its keep next
 * to a jump-to-end button.
 *
 * The whole thing renders even on a single page, because the row count and the
 * page-size control are useful when there is nothing to page through — but the
 * buttons disable rather than disappear, so the layout does not shift as the
 * result shrinks and grows.
 */
export default function Pager<T>({
  page,
  onPageChange,
  onPageSizeChange,
  noun = 'items',
}: PagerProps<T>) {
  const { page: current, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage } = page;

  const firstOnPage = totalCount === 0 ? 0 : (current - 1) * pageSize + 1;
  const lastOnPage = Math.min(current * pageSize, totalCount);

  return (
    <nav className={styles.pager} aria-label="Pagination">
      {/* aria-live so a screen reader hears the new range after a page change,
          which is otherwise a silent update to the middle of the document. */}
      <p className={styles.range} aria-live="polite">
        {totalCount === 0
          ? `No ${noun}`
          : `Showing ${firstOnPage}–${lastOnPage} of ${totalCount} ${noun}`}
      </p>

      <div className={styles.controls}>
        <label className={styles.pageSize}>
          <span className={styles.pageSizeLabel}>Per page</span>
          <select
            className={ui.select}
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
          >
            {PAGE_SIZE_OPTIONS.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>

        <div className={styles.buttons}>
          <button
            type="button"
            className={`${ui.button} ${ui.secondary}`}
            onClick={() => onPageChange(1)}
            disabled={!hasPreviousPage}
            aria-label="First page"
          >
            «
          </button>

          <button
            type="button"
            className={`${ui.button} ${ui.secondary}`}
            onClick={() => onPageChange(current - 1)}
            disabled={!hasPreviousPage}
          >
            Previous
          </button>

          <span className={styles.position}>
            Page {current} of {Math.max(totalPages, 1)}
          </span>

          <button
            type="button"
            className={`${ui.button} ${ui.secondary}`}
            onClick={() => onPageChange(current + 1)}
            disabled={!hasNextPage}
          >
            Next
          </button>

          <button
            type="button"
            className={`${ui.button} ${ui.secondary}`}
            onClick={() => onPageChange(totalPages)}
            disabled={!hasNextPage}
            aria-label="Last page"
          >
            »
          </button>
        </div>
      </div>
    </nav>
  );
}
