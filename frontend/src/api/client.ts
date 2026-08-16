/**
 * The single place the frontend talks to the API.
 *
 * Everything goes through `request`, so the base URL, the credentials policy,
 * and the translation of RFC 9457 ProblemDetails into a readable error are each
 * written once rather than at every call site.
 *
 * There is no token variable here, and that is the point: the session is an
 * HttpOnly cookie the browser attaches itself, so this code — and any other
 * script that ends up on the page — has no way to read the credential it is
 * sending.
 */

const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5099').replace(/\/$/, '');

/** A failed request, carrying the messages the API considered safe to show. */
export class ApiError extends Error {
  // Declared as fields rather than constructor parameter properties: the
  // project builds with `erasableSyntaxOnly`, so type-only syntax that emits
  // runtime code is rejected.
  readonly status: number;
  readonly messages: string[];

  constructor(status: number, messages: string[]) {
    super(messages[0] ?? `The request failed (${status}).`);
    this.name = 'ApiError';
    this.status = status;
    this.messages = messages;
  }

  /** True when the caller needs to sign in (or sign in again). */
  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  /** True when the caller is signed in but not allowed to do this. */
  get isForbidden(): boolean {
    return this.status === 403;
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/**
 * Pulls the useful text out of a ProblemDetails body.
 *
 * Validation failures arrive under `errors` as arrays keyed by field; every
 * other failure carries a single `detail`. Flattening both to a string list
 * lets the UI render them the same way.
 */
async function readErrorMessages(response: Response): Promise<string[]> {
  try {
    const problem = (await response.json()) as ProblemDetails;

    const fieldErrors = Object.values(problem.errors ?? {}).flat();
    if (fieldErrors.length > 0) return fieldErrors;

    if (problem.detail) return [problem.detail];
    if (problem.title) return [problem.title];
  } catch {
    // A non-JSON body (a proxy error page, an empty 502) is not worth parsing.
  }

  return [`The request failed (${response.status}).`];
}

interface RequestOptions {
  method?: string;
  /** A JSON-serializable body. Mutually exclusive with `form`. */
  body?: unknown;
  /** A multipart body, used for the ticket image upload. */
  form?: FormData;
  signal?: AbortSignal;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};

  // Content-Type is set for JSON only. For FormData the browser must set it
  // itself, because it has to append the multipart boundary.
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${BASE_URL}${path}`, {
    method: options.method ?? 'GET',
    headers,

    // The API is on a different origin from the dev server, so the session
    // cookie is only sent if the request asks for it explicitly.
    credentials: 'include',
    body: options.form ?? (options.body === undefined ? undefined : JSON.stringify(options.body)),
    signal: options.signal,
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorMessages(response));
  }

  // 204, or any response with no body to parse.
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Turns a stored `uploads/x.png` path into a URL the browser can load. */
export function toAbsoluteUrl(storagePath: string): string {
  return `${BASE_URL}/${storagePath.replace(/^\//, '')}`;
}
