import { expect, type APIRequestContext, type Page } from '@playwright/test';
import { API_URL } from './ports';

/**
 * The moves every spec needs, written once.
 *
 * Two rules hold throughout. Anything a test *asserts* goes through the browser,
 * because that is the only way an end-to-end test proves what a user would see.
 * Anything a test merely needs to be *true beforehand* may go through the API,
 * because filing twelve tickets through a modal to test a pager is slow without
 * testing anything the ticket-filing spec has not already covered.
 */

/**
 * The account committed in appsettings.Development.json. Not a secret — the
 * login screen prints it in development — and the suite runs against that same
 * environment, so this is the credential the app itself advertises.
 */
export const ADMIN = {
  email: 'admin@example.com',
  password: 'Admin123!',
} as const;

/**
 * A token no other test will match on.
 *
 * The suite shares one dataset, so "the ticket I just filed" is only findable if
 * it is named something nothing else is. Every list assertion is scoped to one
 * of these rather than to a total, which is what keeps the specs independent of
 * each other and of whatever the seed happens to contain.
 */
export function uniqueTag(prefix = 'e2e'): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
}

/** Signs in through the login screen and waits for the header to agree. */
export async function signIn(page: Page): Promise<void> {
  await page.goto('/login');

  await page.getByLabel('Email address').fill(ADMIN.email);
  await page.getByLabel('Password').fill(ADMIN.password);
  await page.getByRole('button', { name: 'Login' }).click();

  // The header is the app's own statement about who it thinks you are; waiting
  // on it means later steps cannot race the sign-in they depend on.
  await expect(page.getByText(`${ADMIN.email} · admin`)).toBeVisible();
}

/** Fills and submits the New Ticket modal, returning what was typed into it. */
export async function fileTicket(
  page: Page,
  ticket: { name: string; email: string; description: string },
): Promise<void> {
  await page.getByRole('button', { name: 'New Ticket' }).click();

  const modal = page.getByRole('dialog');
  await expect(modal).toBeVisible();

  await modal.getByLabel('Name').fill(ticket.name);
  await modal.getByLabel('Email address').fill(ticket.email);
  await modal.getByLabel('Description').fill(ticket.description);

  await modal.getByRole('button', { name: 'Submit' }).click();
}

/** The fields of a ticket the API hands back. Only what the specs read. */
export interface SeededTicket {
  id: string;
  name: string;
  email: string;
  status: string;
}

/**
 * Files one ticket straight at the API, for tests that need a ticket to exist
 * rather than tests that are about the filing of it.
 */
export async function createTicket(
  request: APIRequestContext,
  name: string,
  description = `A ticket filed by ${name} for an end-to-end test.`,
): Promise<SeededTicket> {
  const response = await request.post(`${API_URL}/api/tickets`, {
    data: { name, email: 'seed@example.com', description },
  });

  expect(response.status(), 'seeding a ticket').toBe(201);

  return (await response.json()) as SeededTicket;
}

/** The same, in bulk — a list long enough to page through. */
export async function seedTickets(
  request: APIRequestContext,
  count: number,
  name: string,
): Promise<void> {
  for (let index = 1; index <= count; index++) {
    await createTicket(request, name, `Seeded ticket ${index} of ${count} for ${name}.`);
  }
}

/**
 * The list row for a ticket with this name.
 *
 * Located by the link role rather than the row role, which is not an oversight:
 * each `<tr>` in the list carries an explicit `role="link"` because the whole
 * row navigates when clicked, and an explicit role replaces the implicit one. To
 * a browser's accessibility tree — and so to a screen reader, and so to this
 * locator — these are links that happen to be laid out as a table.
 */
export function ticketRow(page: Page, name: string) {
  return page.getByRole('link').filter({ hasText: name });
}

/**
 * The issue description on the detail page.
 *
 * Scoped to its own section rather than found by its text, because the run uses
 * the stub summariser and a short description comes back as its own summary —
 * so the same words are on the page twice, under two different headings.
 */
export function issueDescription(page: Page) {
  return page
    .locator('section')
    .filter({ has: page.getByRole('heading', { name: 'Issue description' }) })
    .getByRole('paragraph');
}

/** Narrows the list to one tag and waits for the table to settle on it. */
export async function searchFor(page: Page, term: string): Promise<void> {
  await page.getByLabel('Search tickets').fill(term);

  // The search box is debounced, so the request trails the typing. Waiting on a
  // row rather than a timeout keeps that an implementation detail.
  await expect(ticketRow(page, term).first()).toBeVisible();
}
