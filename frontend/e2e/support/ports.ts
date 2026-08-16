/**
 * The two addresses an end-to-end run uses.
 *
 * Deliberately not 5099 and 5173: a run must not collide with a dev server
 * someone already has open, and — worse — must not quietly test against one.
 * Shared by playwright.config.ts, which starts the servers, and by the tests
 * that talk to the API directly to arrange state.
 */

export const API_PORT = 5199;
export const WEB_PORT = 5273;

export const API_URL = `http://127.0.0.1:${API_PORT}`;
export const WEB_URL = `http://127.0.0.1:${WEB_PORT}`;
