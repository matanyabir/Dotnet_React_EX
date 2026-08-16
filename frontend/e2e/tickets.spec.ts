import { expect, test } from '@playwright/test';
import { createTicket, issueDescription, searchFor, ticketRow, uniqueTag } from './support/app';

/**
 * Browsing tickets without signing in.
 *
 * The exercise is explicit that filing and reading are open to everyone and only
 * editing is not, so the first thing worth proving is that the open door is
 * genuinely open: a visitor with no session can reach the list, narrow it, and
 * open any ticket by its id.
 */
test.describe('the tickets list, signed out', () => {
  test('serves the dataset it was seeded with', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('link', { name: /Ticket Management/ })).toBeVisible();

    // Searched for rather than expected on the first page: by the time this file
    // runs, earlier specs have filed enough tickets to push the seed — which is
    // the oldest, and the list is newest-first — onto a later page. Asserting it
    // was on page 1 would be asserting the order other specs happened to run in.
    await searchFor(page, 'John Doe');

    const john = ticketRow(page, 'John Doe');
    await expect(john).toContainText('john.doe@example.com');
    await expect(john).toContainText('New');
  });

  test('filters by status, both ways', async ({ page, request }) => {
    // Self-contained: one ticket of this test's own, whose status it knows. A
    // filter test written against the seed would depend on nothing else in the
    // suite having changed a status, which admin-edit.spec does.
    const name = uniqueTag('Filterable');
    await createTicket(request, name);

    await page.goto('/');
    await searchFor(page, name);

    // Filed as New, so a Closed filter must take it away...
    await page.getByLabel('Filter by status').selectOption('Closed');
    await expect(ticketRow(page, name)).toHaveCount(0);

    // ...and a New filter must bring it back. Only asserting the disappearance
    // would also pass on a filter that matched nothing at all.
    await page.getByLabel('Filter by status').selectOption('New');
    await expect(ticketRow(page, name)).toBeVisible();
  });

  test('searches by name', async ({ page, request }) => {
    // A name nothing else in the dataset shares, so the assertion cannot be
    // satisfied by a row some other test left behind.
    const name = uniqueTag('Searchable');
    await createTicket(request, name);

    await page.goto('/');
    await searchFor(page, name);

    await expect(ticketRow(page, name)).toHaveCount(1);
    await expect(ticketRow(page, 'John Doe')).toHaveCount(0);
  });

  test('says so when a search matches nothing', async ({ page }) => {
    await page.goto('/');

    await page.getByLabel('Search tickets').fill(uniqueTag('nothing-matches-this'));

    await expect(page.getByText('No tickets match those filters')).toBeVisible();
  });

  test('opens a ticket from its row', async ({ page, request }) => {
    const name = uniqueTag('Clickable');
    const ticket = await createTicket(request, name, 'The dishwasher sings at night.');

    await page.goto('/');
    await searchFor(page, name);
    await ticketRow(page, name).click();

    await expect(page).toHaveURL(`/tickets/${ticket.id}`);
    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(issueDescription(page)).toHaveText('The dishwasher sings at night.');
    await expect(page.getByText(`Reference: ${ticket.id}`)).toBeVisible();
  });

  test('reaches a ticket by id on a cold load', async ({ page, request }) => {
    // The link the confirmation email sends the customer. It arrives with no app
    // state behind it, which is the case this covers: everything on the screen
    // has to come from the id in the URL.
    const name = uniqueTag('Emailed');
    const ticket = await createTicket(request, name);

    await page.goto(`/tickets/${ticket.id}`);

    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(page.getByText('Issue description')).toBeVisible();
  });

  test('shows a summary generated for the ticket', async ({ page, request }) => {
    // The run configures the stub summariser, so this proves the summary makes
    // the round trip and renders — not that any particular model said anything.
    const name = uniqueTag('Summarised');
    const ticket = await createTicket(request, name, 'The oven door will not stay shut.');

    await page.goto(`/tickets/${ticket.id}`);

    await expect(page.getByText('AI summary')).toBeVisible();
  });

  test('explains an id that does not exist', async ({ page }) => {
    // Well-formed but unknown, so the API answers 404 and the page has to say
    // something better than a blank screen.
    await page.goto('/tickets/00000000-0000-0000-0000-000000000000');

    await expect(
      page.getByText('No ticket with that reference. Check the link, or browse all tickets.'),
    ).toBeVisible();

    await page.getByRole('link', { name: 'Back to all tickets' }).click();
    await expect(page).toHaveURL('/');
  });

  test('offers no way to edit', async ({ page, request }) => {
    const name = uniqueTag('ReadOnly');
    const ticket = await createTicket(request, name);

    await page.goto(`/tickets/${ticket.id}`);

    await expect(page.getByRole('button', { name: 'Save changes' })).toHaveCount(0);
    await expect(page.getByLabel('Resolution')).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Sign in as an admin' })).toBeVisible();
  });
});
