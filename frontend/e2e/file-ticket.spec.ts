import { expect, test } from '@playwright/test';
import { fileTicket, issueDescription, searchFor, ticketRow, uniqueTag } from './support/app';

/**
 * Filing a ticket — the thing the application exists for, and the one journey
 * that must work for someone who has never signed in.
 */
test.describe('filing a ticket', () => {
  test('files one and shows it in the list', async ({ page }) => {
    const name = uniqueTag('Filed');

    await page.goto('/');
    await fileTicket(page, {
      name,
      email: 'filed@example.com',
      description: 'The kettle boils but never switches itself off.',
    });

    // The customer is told where the tracking link went — this is the only place
    // the app promises an email was sent, so it is worth pinning.
    await expect(page.getByText('created', { exact: false })).toBeVisible();
    await expect(page.getByText('filed@example.com', { exact: false }).first()).toBeVisible();

    // The modal closes and the new ticket is on the list behind it, open.
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(ticketRow(page, name)).toBeVisible();
    await expect(ticketRow(page, name)).toContainText('New');
  });

  test('the filed ticket opens with what was typed into it', async ({ page }) => {
    const name = uniqueTag('Roundtrip');
    const description = 'The freezer defrosts itself every Tuesday without being asked.';

    await page.goto('/');
    await fileTicket(page, { name, email: 'roundtrip@example.com', description });

    await searchFor(page, name);
    await ticketRow(page, name).click();

    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(issueDescription(page)).toHaveText(description);
  });

  test('refuses an empty form and says which fields are missing', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'New Ticket' }).click();

    const modal = page.getByRole('dialog');
    await modal.getByRole('button', { name: 'Submit' }).click();

    await expect(modal.getByText('Please tell us your name.')).toBeVisible();
    await expect(modal.getByText('Please give us an email address.')).toBeVisible();
    await expect(modal.getByText('Please describe what went wrong.')).toBeVisible();

    // Still open, and nothing filed — being thrown out of the form having lost
    // what you typed would be the worse failure.
    await expect(modal).toBeVisible();
  });

  test('refuses an address that is not one', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'New Ticket' }).click();

    const modal = page.getByRole('dialog');
    await modal.getByLabel('Name').fill('Malformed Address');
    await modal.getByLabel('Email address').fill('not-an-email');
    await modal.getByLabel('Description').fill('Something is broken.');
    await modal.getByRole('button', { name: 'Submit' }).click();

    await expect(modal.getByText('That does not look like an email address.')).toBeVisible();
  });

  test('keeps the person but clears the problem, ready for the next one', async ({ page }) => {
    // The modal keeps the form mounted, so state outlives a submit. The second
    // ticket is the same person with a different problem, so the name and email
    // stay and the description does not — filing a duplicate description by
    // accident is the bug this prevents.
    const name = uniqueTag('Returning');

    await page.goto('/');
    await fileTicket(page, {
      name,
      email: 'returning@example.com',
      description: 'The first thing that went wrong.',
    });

    await expect(page.getByRole('dialog')).toBeHidden();
    await page.getByRole('button', { name: 'New Ticket' }).click();

    const modal = page.getByRole('dialog');
    await expect(modal.getByLabel('Name')).toHaveValue(name);
    await expect(modal.getByLabel('Email address')).toHaveValue('returning@example.com');
    await expect(modal.getByLabel('Description')).toHaveValue('');
  });

  test('can be abandoned', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'New Ticket' }).click();

    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();

    await modal.getByRole('button', { name: 'Cancel' }).click();

    await expect(modal).toBeHidden();
  });
});
