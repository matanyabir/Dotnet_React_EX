import { expect, test } from '@playwright/test';
import { ADMIN } from './support/app';
import { API_URL } from './support/ports';

/**
 * The sign-in rate limit, seen from a browser.
 *
 * **This file runs last, and the name is what makes that happen.** It spends the
 * run's whole sign-in allowance on purpose, and the limit counts by client
 * address — so every test in the suite shares one bucket, and anything that
 * signed in after this would be refused. The suite runs with a single worker, in
 * file order, which is what keeps "last" true.
 *
 * The limiter's own behaviour — the boundary, the header, the ProblemDetails
 * shape — is covered by MX.Api.Tests/LoginRateLimitTests.cs, and repeating that
 * here would be slower and prove less. What only a browser can show is the part
 * that spans both sides: that the 429 is *readable* to the page at all.
 *
 * That is not free. A cross-origin response is opaque to script unless CORS says
 * otherwise, so a rate limiter placed ahead of the CORS middleware would answer
 * with a 429 the page could not read, and the user would get "could not reach
 * the server" — the API looking broken at exactly the moment it is working
 * correctly. `UseRateLimiter` sits after `UseCors` for that reason, and this is
 * the test that would notice if it ever moved.
 */
test.describe('the sign-in rate limit', () => {
  test('refuses a throttled sign-in in words the page can show', async ({ page, request }) => {
    // Spend the allowance from outside the browser: same client address, so the
    // same bucket, and far quicker than driving the form. The loop is bounded
    // rather than counted because earlier specs have already spent some of it.
    let throttled = false;

    for (let attempt = 0; attempt < 80 && !throttled; attempt++) {
      const response = await request.post(`${API_URL}/api/auth/login`, {
        data: { email: ADMIN.email, password: 'not-the-password' },
      });

      throttled = response.status() === 429;
    }

    expect(throttled, 'the allowance should run out well inside 80 attempts').toBe(true);

    // Now the part that needs a browser. Correct credentials, offered through
    // the real form: the limit is checked before the password, so this is
    // refused too — and the reason has to arrive as text on the screen.
    await page.goto('/login');
    await page.getByLabel('Email address').fill(ADMIN.email);
    await page.getByLabel('Password').fill(ADMIN.password);
    await page.getByRole('button', { name: 'Login' }).click();

    await expect(page.getByRole('alert')).toContainText('Too many sign-in attempts');

    // The generic fallback the client falls back to when it cannot read a body.
    // Seeing it here would mean the 429 arrived opaque.
    await expect(page.getByRole('alert')).not.toContainText('Could not reach the server');

    // Still signed out, which is the whole point of refusing a correct password.
    await expect(page).toHaveURL('/login');
    await expect(page.getByRole('button', { name: 'Sign out' })).toHaveCount(0);
  });
});
