# Smart Customer Support System

A customer-support ticket system: anyone can file a ticket, an admin can triage it.
ASP.NET Core Minimal API backend, React frontend, tickets stored in the supplied
`dataset.json`.

Built for the exercise brief in **[EXERCISE.md](EXERCISE.md)**. The stage-by-stage
plan it was built to is in **[PLAN.md](PLAN.md)**.

---

## Quick start

Two terminals. No API keys, no database, no configuration.

```bash
# Terminal 1 — API on http://localhost:5099
dotnet run --project backend/MX.Api

# Terminal 2 — UI on http://localhost:5173
cd frontend && npm install && npm run dev
```

Open <http://localhost:5173>. Sign in with **`admin@example.com`** / **`Admin123!`**
to edit tickets. Simulated emails print to the API console.

**Prerequisites:** .NET SDK 9.0.200 or newer (the `.slnx` solution format needs it)
and Node.js 20+.

> If something already occupies port 5173, Vite moves to 5174 and everything still
> works — in development the API accepts any loopback origin.

---

## What it does

| | |
|---|---|
| **File a ticket** | Anonymous. Name, email, description, optional image. |
| **Track a ticket** | `/tickets/{id}` — the link sent in the confirmation email. Works on a cold load. |
| **Page through them** | 20 a page by default, or 10/50/100. Composes with the filters. |
| **Triage** | Admin only: change status, write a resolution. |
| **Notify** | An email on create, on status change, and on resolution change — and on nothing else. |
| **Summarise** | A one-line AI précis of each new ticket. |

### Screenshots

| Tickets list | Admin login |
|---|---|
| ![Tickets list](docs/screenshots/tickets-list.jpg) | ![Login](docs/screenshots/login.jpg) |

Both were captured at a 700px-wide viewport, so the list shows the responsive card
layout rather than the wide table. Above 760px the same list renders as the table in
[`docs/ticket-manage.png`](docs/ticket-manage.png). Both predate the pager, which sits
below the last row.

---

## Running the tests

```bash
dotnet test                      # 264 tests
cd frontend && npm run e2e       # 32 tests, in a real browser
```

```
MX.Application.Tests   100   use cases and domain rules, every port mocked
MX.Infrastructure.Tests 74   JSON persistence, password hashing, uploads, AI resilience
MX.Api.Tests            90   real HTTP through the real pipeline, temp dataset copy
frontend/e2e            32   Playwright: a browser against both halves at once
```

The frontend is also checked by its build — `cd frontend && npm run build` —
which type-checks the DTOs against what the API actually returns, and now the
end-to-end specs along with them.

### The end-to-end suite

`npm run e2e` starts everything it needs: the API on **5199** and Vite on
**5273**, both seeded and torn down by Playwright. Deliberately not 5099 and
5173, so a run cannot collide with a dev server you already have open — or,
worse, quietly test against it and your data. The API is pointed at a temp copy
of the pristine `dataset.json`, the same seed the .NET tests use, so a run that
files and closes tickets leaves nothing in `git diff`.

Nothing is mocked. These tests exist for the failures that only appear once both
halves are wired together, which is why a browser is worth the setup cost:

- **The session is an HttpOnly cookie**, so no script can read it, and "does a
  refresh keep me signed in" is a question only a real cookie jar can answer.
  Signing out is a server round-trip for the same reason, and is checked to
  actually end the session rather than only hide the header.
- **Same host, different port.** Vite is pinned to `127.0.0.1` rather than its
  default `localhost`, because those are *different sites* to a browser even on
  loopback — `SameSite=Lax` would strip the session cookie from every request
  and the admin would never stay signed in.
- **A 429 has to be readable.** `zz-login-rate-limit.spec.ts` spends the run's
  sign-in allowance for real and checks the rejection arrives as words on the
  screen. A rate limiter placed ahead of the CORS middleware would answer with a
  429 the page cannot read, and the user would be told the server is unreachable
  at the exact moment it is working correctly. That file is named to run last,
  because the allowance it spends is shared by every test before it.

First run downloads a browser (`npx playwright install chromium`, ~95 MB).

---

## Architecture

Four projects, with the dependency rule pointing inward. `MX.Domain` references
nothing at all, so business rules cannot leak into HTTP or storage concerns.

```
backend/
  MX.Domain          entities, domain events, the status vocabulary — zero dependencies
  MX.Application     use cases, DTOs, and the ports (interfaces) infrastructure implements
  MX.Infrastructure  the adapters: JSON store, email, AI, JWT, file storage
  MX.Api             Minimal API endpoints and the DI composition root
frontend/            Vite + React + TypeScript
```

This is enforced, not just intended: `ArchitectureTests` reads the compiled
assemblies and fails the build if `MX.Domain` ever references ASP.NET, or if
`MX.Application` reaches outward to Infrastructure. Those tests assert an *absence*,
so a positive control also asserts the probe sees real metadata — otherwise they
could pass by inspecting nothing.

### The patterns, and why each is there

| Pattern | Where | Why it earns its place |
|---|---|---|
| **Repository** | `JsonTicketRepository` | Isolates the file mechanics so services test without touching disk. |
| **Domain events** | `TicketCreated`, `TicketStatusChanged`, `TicketResolutionChanged` | The three email cases become three handlers, not three `if` blocks in the service. |
| **Strategy** | `IEmailSender`, `ISummaryGenerator` | Mock and real implementations swap by config, no call-site change. |
| **Decorator** | `ResilientSummaryGenerator` | Makes "must not throw, must not hang" true in one place. |
| **Result** | `Result<T>` | "Not found" and "invalid" are return values, so a 404 costs no stack trace. |
| **Options** | `JwtOptions`, `EmailOptions`, `AiOptions`, `StorageOptions` | Typed config; the integration tests point the app at a temp dataset through it. |

### Decisions worth knowing about

**The entity decides what counts as a change.** `ChangeStatus` and `SetResolution`
return whether anything actually changed and raise an event only if it did. That is
what makes "pressing Save twice does not email the customer twice" a property of the
model rather than something every caller must remember.

**`"In Progress"` has a space in it.** A plain C# enum cannot round-trip that, so
`TicketStatusJsonConverter` handles it and `TicketStatusNames` is the single place the
spelling lives — shared by the JSON store and the HTTP API so the two cannot drift.
`GET /api/tickets/statuses` serves that same list to the frontend, so the dropdowns
cannot drift either. A test rewrites the supplied `dataset.json` and requires the
bytes to come back identical, which pins the status spelling, the timestamp format,
property order, and character encoding all at once.

**The list is paged, and a bad page is a 400 rather than a guess.** `GET
/api/tickets` returns one page inside an envelope carrying `totalCount`, so the UI
can say "page 2 of 9" instead of only offering a "next". Two choices are worth
naming. `pageSize` is capped at 100 and an over-large value is *rejected*, not
clamped — silently returning a tenth of what was asked for is a bug that surfaces
as missing data much later. A page *past* the end, though, is an empty page and a
200: that is what happens when a filter shrinks the result under someone, and it
is not their mistake. The sort is stable, which paging depends on — two tickets
sharing a timestamp must not swap places between the request for page 1 and the
request for page 2, or one of them appears twice and the other never.

**Uploads are treated as hostile.** The stored filename is generated rather than taken
from the upload, which removes path traversal instead of filtering for it; the image
format is decided from the file's leading bytes, not its declared content type; and
the read is bounded so an oversized body is rejected before being buffered whole.

**Editing lives on the detail screen only.** `ticket-manage.png` shows status and
resolution editable inline in the table, but the brief's functional requirements put
editing on the detail view. One editing surface is easier to keep correct than two.

---

## Configuration

Everything ships working with no configuration. Each integration can be switched to
its real implementation independently.

### AI summaries — `Ai:Provider`

| Value | Behaviour |
|---|---|
| `Stub` *(default)* | Deterministic local truncation. No network, no key, no cost. |
| `Claude` | Calls the Anthropic API. Needs `Ai:ApiKey`. |
| `None` | Feature off. |

```bash
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..." --project backend/MX.Api
# then set "Ai:Provider": "Claude" in appsettings.Development.json
```

A failure never costs more than the summary: `ResilientSummaryGenerator` applies a
timeout and turns any provider error into "no summary", so the ticket is still created.
Verified with a deliberately invalid key — the ticket returned 201 in under a second.

Verified against the live Anthropic API on 2026-08-16 (`claude-opus-5`). The same
description, summarised by each provider:

| Provider | Output |
|---|---|
| `Stub` | `i put my cup of coffee near my mac, and my cat push the cup on my mac, and now the screen is not working` |
| `Claude` | `MacBook screen not working after coffee spilled on it.` |

The stub returns the first sentence, so on a single-sentence description it echoes the
input verbatim — the feature looks inert when it is working normally. Worth knowing
before concluding that summarisation is broken.

### Email — `Email:Provider`

| Value | Behaviour |
|---|---|
| `Mock` *(default)* | Logs the message to the console and records it in memory. |
| `Smtp` | Sends over SMTP via MailKit. |

```bash
dotnet user-secrets set "Email:Smtp:Username" "you@gmail.com" --project backend/MX.Api
dotnet user-secrets set "Email:Smtp:Password" "<16-char app password>" --project backend/MX.Api
```

Verified against a live mail server on 2026-08-16 (Mailtrap sandbox, `LOGIN` auth and
STARTTLS on port 2525). All three notification events delivered with correct MIME
headers and a UTF-8 body: confirmation on creation, status change, and resolution added.

Note that `Mock` writes to the API's console and nowhere else — no message reaches an
inbox until `Email:Provider` is `Smtp`.

### Auth

Development credentials live in `appsettings.Development.json` and are committed so the
exercise runs straight after a clone. The password is stored as a PBKDF2 hash, never
plaintext. No signing key ships in `appsettings.json` — startup fails with a clear
message if one is missing outside development.

```bash
dotnet user-secrets set "Auth:Jwt:SigningKey" "<32+ random characters>" --project backend/MX.Api
```

The token itself is returned as an `HttpOnly` cookie and never in the response body,
so no code on the page — including anything injected into it — can read the
credential it is sending. The frontend therefore has no token to inspect and calls
`GET /api/auth/me` to learn who it is signed in as. `Authorization: Bearer` is still
accepted for callers that are not browsers.

`POST /api/auth/login` is rate limited, because it is the only route that is both
anonymous by necessity and worth attacking by repetition — and every wrong guess
costs the API a deliberately expensive PBKDF2 verification. Ten attempts per client
address per five minutes (`Auth:LoginRateLimit`); past that the endpoint answers
`429` with a `Retry-After` header and stops checking credentials at all, so a correct
password guessed on the eleventh try is no more useful than a wrong one. The count is
per address rather than per account on purpose: keying it to the submitted email
would let anyone lock a named admin out by failing logins on their behalf.

Cookies travel automatically, which is the opening CSRF uses, so three things close
it and all three have to hold: `SameSite=Lax` keeps the cookie off cross-site
state-changing requests, editing requires a JSON content type that a forged form
cannot set without a preflight, and the CORS policy names the origins it will accept
credentials from. `Secure` and the `__Host-` cookie prefix are applied whenever the
request arrives over HTTPS.

### Storage

`Storage:DataFilePath` defaults to `Data/dataset.json` relative to the API's content
root, so tickets you create show up in `git diff` — the persistence requirement is
visible rather than asserted. Uploaded images go to `wwwroot/uploads` and are served at
`/uploads/{file}`.

---

## API

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/auth/login` | — | Email and password for a session cookie. Rate limited; `429` past the cap. |
| `POST` | `/api/auth/logout` | — | Clears the session cookie. |
| `GET` | `/api/auth/me` | signed in | Who the caller's session belongs to. |
| `GET` | `/api/tickets?status=&search=&page=&pageSize=` | — | One page of the list, filtered. `status=All` or omitted means no filter. |
| `GET` | `/api/tickets/statuses` | — | The status vocabulary, for dropdowns. |
| `GET` | `/api/tickets/{id}` | — | One ticket by id. |
| `POST` | `/api/tickets` | — | File a ticket. `multipart/form-data` (with an optional `image`) or JSON. |
| `PUT` | `/api/tickets/{id}` | **admin** | Update `status` and/or `resolution`. |
| `GET` | `/uploads/{file}` | — | An uploaded image. |
| `GET` | `/health` | — | Liveness. |

Swagger UI is at `/swagger` in development. Failures come back as RFC 9457
ProblemDetails, the same shape ASP.NET produces for unhandled errors, so a client
parses one error format.

On `PUT`, omitting a field leaves it unchanged; sending `""` for `resolution` clears it.
That distinction is what lets status and resolution be edited independently.

The list endpoint answers with a page, not a bare array. `page` defaults to 1 and
`pageSize` to 20, with a maximum of 100; both are optional, so a client that ignores
paging still works and simply gets the first page.

```jsonc
{
  "items": [ /* … TicketDto … */ ],
  "page": 2,
  "pageSize": 20,
  "totalCount": 53,      // the whole match, not this page
  "totalPages": 3,
  "hasPreviousPage": true,
  "hasNextPage": true
}
```

```bash
# The second page of closed tickets, ten at a time
curl -s "http://localhost:5099/api/tickets?status=Closed&page=2&pageSize=10"
```

```bash
# File a ticket with an image
curl -X POST http://localhost:5099/api/tickets \
  -F "name=Ada Lovelace" -F "email=ada@example.com" \
  -F "description=The printer is on fire." -F "image=@photo.png"

# Sign in, then close a ticket. The token comes back as a cookie rather than in
# the body, so the jar is what carries the session from one call to the next.
curl -s -c jar.txt -X POST http://localhost:5099/api/auth/login \
  -H 'Content-Type: application/json' \
  --data-binary '{"email":"admin@example.com","password":"Admin123!"}'

curl -X PUT "http://localhost:5099/api/tickets/$ID" \
  -b jar.txt -H 'Content-Type: application/json' \
  -d '{"status":"Resolved","resolution":"Replaced the drain pump."}'
```

---

## Known limitations

- **Paging is applied in memory, after the whole file is read.** The client is
  protected — a request can never ask for more than 100 rows — but the server is not:
  `JsonTicketRepository` has no way to fetch a slice, so a dataset large enough to
  strain the process still gets loaded whole on every list. Pushing `Skip`/`Take`
  down to the store is the same boundary a real database would cross, and the
  `ITicketRepository` port is where that change would land.
- **Concurrent writes** are serialised through a semaphore in a single process, and the
  file is replaced atomically so a crash cannot truncate it. That is correct for one
  instance and would not survive being scaled out — the point at which a real database
  earns its place.
- **The session cookie assumes the UI and the API are same-site.** `SameSite=Lax` is
  what keeps the cookie off a cross-site forged request, and it also means the cookie
  is not sent at all if the two are ever deployed to genuinely different sites — the
  frontend would look permanently signed out. Ports do not count, so
  `localhost:5173` → `localhost:5099` is fine, as is `app.example.com` →
  `api.example.com`. A split across registrable domains would need `SameSite=None`
  plus a CSRF token to replace the protection that gives up.
- **Nothing revokes a token before it expires.** Signing out deletes the cookie, which
  ends the session for that browser, but the token stays valid until its expiry if it
  was captured beforehand. The `jti` claim exists so a denylist could be added; there
  is no store behind it yet.
- **The login rate limit counts the connecting address, and is per process.** Behind a
  reverse proxy that address is the proxy, which collapses every client into one
  bucket — deploying that way needs `UseForwardedHeaders` with the proxy named in
  `KnownProxies`, left out here because enabling it without pinning the trusted proxy
  lets any caller spoof `X-Forwarded-For` and mint a fresh bucket per request. The
  counters also live in memory, so scaling out multiplies the effective limit by the
  instance count; a shared store is the same boundary the ticket data crosses.
- **The supplied dataset references images that were never shipped**
  (`uploads/laptop_issue.jpg` and friends), so those attachments 404. The UI says so
  rather than showing a broken-image icon.
- **Notifications are sent inline and never retried.** A handler failure is caught and
  logged, so a dead mail server costs the notification and not the ticket — but the
  customer is never told, and nothing tries again. Observed for real: when one `PUT`
  changes status *and* resolution, both emails go out back-to-back and a provider
  throttle can reject the second (Mailtrap's free tier answers `5.7.0 Too many emails
  per second`). The ticket saved correctly and the loss appeared only in the log.
  Handing sends to a queue with retry is the fix, and `IEmailSender` is where it lands.
- **The responsive layout below 760px** is exercised in the screenshot above; the wide
  table was verified separately during development.
