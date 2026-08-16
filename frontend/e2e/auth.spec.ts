import { expect, test } from '@playwright/test';
import { ADMIN, createTicket, signIn, uniqueTag } from './support/app';

/**
 * Signing in, staying signed in, and signing out.
 *
 * This is the part a browser is genuinely needed for. The session is an HttpOnly
 * cookie: no script on the page can read it, so nothing below can be faked by
 * setting a variable, and "does a refresh keep me signed in" is a question only
 * a real browser with a real cookie jar can answer.
 */
test.describe('sessions', () => {
  test('a visitor is offered a sign-in and nothing else', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('link', { name: 'Admin sign in' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign out' })).toHaveCount(0);
  });

  test('a wrong password is refused, in words', async ({ page }) => {
    await page.goto('/login');

    await page.getByLabel('Email address').fill(ADMIN.email);
    await page.getByLabel('Password').fill('not-the-password');
    await page.getByRole('button', { name: 'Login' }).click();

    // The server's own message, which means the ProblemDetails body survived the
    // trip and was read — a banner reading "the request failed (401)" would mean
    // the client never parsed it.
    await expect(page.getByRole('alert')).toContainText('Email address or password is incorrect.');
    await expect(page).toHaveURL('/login');
  });

  test('an unknown account is refused the same way', async ({ page }) => {
    // Identical wording to the test above, on purpose: the API answers both
    // cases the same so the login screen cannot be used to discover which
    // accounts exist. A friendlier "no such user" here would undo that.
    await page.goto('/login');

    await page.getByLabel('Email address').fill('nobody@example.com');
    await page.getByLabel('Password').fill(ADMIN.password);
    await page.getByRole('button', { name: 'Login' }).click();

    await expect(page.getByRole('alert')).toContainText('Email address or password is incorrect.');
  });

  test('the right password signs you in and says who you are', async ({ page }) => {
    await signIn(page);

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
  });

  test('the session survives a reload', async ({ page }) => {
    // The token is in a cookie the page cannot read, so restoring a session is
    // a server round trip (GET /api/auth/me). If that ever breaks, every refresh
    // silently signs the admin out.
    await signIn(page);

    await page.reload();

    await expect(page.getByText(`${ADMIN.email} · admin`)).toBeVisible();
  });

  test('the session reaches a page opened cold', async ({ page, request }) => {
    const name = uniqueTag('DeepLink');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.goto(`/tickets/${ticket.id}`);

    // Admin-only chrome on a page that was never navigated to from the list.
    await expect(page.getByText('Update ticket')).toBeVisible();
  });

  test('signing out ends it', async ({ page, request }) => {
    const name = uniqueTag('SignedOut');
    const ticket = await createTicket(request, name);

    await signIn(page);
    await page.getByRole('button', { name: 'Sign out' }).click();

    await expect(page.getByRole('link', { name: 'Admin sign in' })).toBeVisible();

    // Only the server can delete a cookie the page is not allowed to touch, so
    // this is the check that sign-out did more than hide the header: the edit
    // form is gone on a freshly loaded page.
    await page.goto(`/tickets/${ticket.id}`);
    await expect(page.getByRole('button', { name: 'Save changes' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Sign in as an admin' })).toBeVisible();
  });

  test('signing out survives a reload too', async ({ page }) => {
    await signIn(page);
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page.getByRole('link', { name: 'Admin sign in' })).toBeVisible();

    await page.reload();

    // If the cookie outlived the click, the reload would sign us back in.
    await expect(page.getByRole('link', { name: 'Admin sign in' })).toBeVisible();
  });
});
