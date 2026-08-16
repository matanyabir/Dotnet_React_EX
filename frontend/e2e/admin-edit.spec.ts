import { expect, test } from '@playwright/test';
import { createTicket, signIn, ticketRow, uniqueTag } from './support/app';

/**
 * The admin-only half of the exercise: anyone may file a ticket, only a
 * signed-in admin may edit one.
 *
 * Every test here creates the ticket it edits. Editing a ticket from the seed
 * would make each test depend on the state the last one left, which is the
 * quickest way to a suite that passes alone and fails together.
 */
test.describe('editing a ticket as an admin', () => {
  test('changes the status and records a resolution', async ({ page, request }) => {
    const name = uniqueTag('Editable');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    await page.getByLabel('Status').selectOption('Resolved');
    await page.getByLabel('Resolution').fill('Replaced the thermostat.');
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page.getByText('Saved. The customer has been emailed about the change.')).toBeVisible();
  });

  test('the change is still there after a reload', async ({ page, request }) => {
    // The list and the detail page both read from the API, so a change that only
    // lives in React state would look saved right up until someone refreshed.
    const name = uniqueTag('Persisted');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    await page.getByLabel('Status').selectOption('In Progress');
    await page.getByLabel('Resolution').fill('Engineer booked for Thursday.');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Saved. The customer has been emailed about the change.')).toBeVisible();

    await page.reload();

    await expect(page.getByLabel('Status')).toHaveValue('In Progress');
    await expect(page.getByLabel('Resolution')).toHaveValue('Engineer booked for Thursday.');
  });

  test('nothing to save leaves the button inert', async ({ page, request }) => {
    // Saving an unchanged ticket would email the customer about nothing, so the
    // button stays disabled until something actually differs — and goes back to
    // disabled when the edit is taken away again.
    const name = uniqueTag('Unchanged');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    const save = page.getByRole('button', { name: 'Save changes' });
    await expect(save).toBeDisabled();

    await page.getByLabel('Resolution').fill('Something');
    await expect(save).toBeEnabled();

    await page.getByLabel('Resolution').fill('');
    await expect(save).toBeDisabled();
  });

  test('discard puts the form back the way it was', async ({ page, request }) => {
    const name = uniqueTag('Discarded');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    await page.getByLabel('Status').selectOption('Closed');
    await page.getByLabel('Resolution').fill('Typed by mistake.');

    await page.getByRole('button', { name: 'Discard' }).click();

    await expect(page.getByLabel('Status')).toHaveValue('New');
    await expect(page.getByLabel('Resolution')).toHaveValue('');
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeDisabled();
  });

  test('the new status shows on the list', async ({ page, request }) => {
    const name = uniqueTag('Listed');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    await page.getByLabel('Status').selectOption('Closed');
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Saved. The customer has been emailed about the change.')).toBeVisible();

    await page.goto('/');
    await page.getByLabel('Search tickets').fill(name);

    await expect(ticketRow(page, name)).toContainText('Closed');
  });
});
