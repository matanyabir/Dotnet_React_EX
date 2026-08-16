import { expect, test } from '@playwright/test';
import { searchFor, seedTickets, uniqueTag } from './support/app';

/**
 * Paging through a list longer than one screen.
 *
 * The tickets are seeded through the API rather than the modal: twelve trips
 * through a form would test nothing the ticket-filing spec has not already
 * covered, and would make this the slowest file in the suite by far.
 *
 * Everything is scoped to a search for one tag, so the totals asserted below are
 * this test's own and not a count of whatever else the run has filed.
 */
test.describe('paging', () => {
  const PER_PAGE = 10;
  const TOTAL = 12;

  test('walks a filtered list a page at a time', async ({ page, request }) => {
    const name = uniqueTag('Paged');
    await seedTickets(request, TOTAL, name);

    await page.goto('/');
    await searchFor(page, name);

    await page.getByLabel('Per page').selectOption(String(PER_PAGE));

    // "matching tickets" rather than "tickets": the list is filtered, and the
    // pager says so, which is what stops the count reading as the whole dataset.
    await expect(page.getByText(`Showing 1–${PER_PAGE} of ${TOTAL} matching tickets`)).toBeVisible();
    await expect(page.getByText('Page 1 of 2')).toBeVisible();

    // Nothing to go back to yet.
    await expect(page.getByRole('button', { name: 'Previous' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'First page' })).toBeDisabled();

    await page.getByRole('button', { name: 'Next' }).click();

    await expect(page.getByText(`Showing ${PER_PAGE + 1}–${TOTAL} of ${TOTAL} matching tickets`)).toBeVisible();
    await expect(page.getByText('Page 2 of 2')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();

    await page.getByRole('button', { name: 'Previous' }).click();
    await expect(page.getByText('Page 1 of 2')).toBeVisible();

    await page.getByRole('button', { name: 'Last page' }).click();
    await expect(page.getByText('Page 2 of 2')).toBeVisible();

    await page.getByRole('button', { name: 'First page' }).click();
    await expect(page.getByText('Page 1 of 2')).toBeVisible();
  });

  test('a bigger page swallows the whole list', async ({ page, request }) => {
    const name = uniqueTag('Resized');
    await seedTickets(request, TOTAL, name);

    await page.goto('/');
    await searchFor(page, name);

    await page.getByLabel('Per page').selectOption(String(PER_PAGE));
    await expect(page.getByText('Page 1 of 2')).toBeVisible();

    await page.getByLabel('Per page').selectOption('20');

    await expect(page.getByText(`Showing 1–${TOTAL} of ${TOTAL} matching tickets`)).toBeVisible();
    await expect(page.getByText('Page 1 of 1')).toBeVisible();
  });

  test('narrowing the search returns you to the first page', async ({ page, request }) => {
    // Otherwise a filter that shrinks the result under you leaves you on page 4
    // of 1, staring at an empty screen that looks like a failure.
    const name = uniqueTag('Rewound');
    await seedTickets(request, TOTAL, name);

    await page.goto('/');
    await searchFor(page, name);
    await page.getByLabel('Per page').selectOption(String(PER_PAGE));

    // Waited for, not assumed: changing the page size sends the list back to
    // page 1, and clicking Next before that lands would be a click the reset
    // then undoes.
    await expect(page.getByText('Page 1 of 2')).toBeVisible();

    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText('Page 2 of 2')).toBeVisible();

    // One seeded description, matched exactly — the search looks in the
    // description as well as the name, so this narrows twelve rows to one.
    await page.getByLabel('Search tickets').fill(`Seeded ticket 3 of ${TOTAL} for ${name}`);

    await expect(page.getByText('Page 1 of 1')).toBeVisible();
    await expect(page.getByText(`Showing 1–1 of 1 matching tickets`)).toBeVisible();
  });
});
