# Smart Customer Support System — Implementation Plan

## Context

`README.md` in our fork (`matanyabir/Dotnet_React_EX`) specifies a full-stack exercise: a customer-support ticket system that lets anyone file a ticket and lets an admin triage it. The repo is a **starter shell** — an empty `MX_EX.slnx`, a 5-record `dataset.json`, three UI mockups, and a `.gitignore`. Every line of code is ours to write.

The exercise is graded on Functionality, **Code Quality**, **Architecture**, UI/UX, and Bonus Features — and the README warns you must be able to explain every line. So the plan optimizes for *defensible design decisions* over raw feature count: clear layering, named patterns, and tests that prove the seams work.

**Decisions locked in:**
- All four bonuses: JWT auth, AI summary, image upload, real SMTP.
- Tests: xUnit backend unit + integration (no frontend/E2E suites).
- Frontend: Vite + React + TypeScript, plain CSS.
- External services: pluggable behind interfaces, **stub implementations are the default** — the app builds, runs, and tests green with zero credentials.

---

## What the source material actually says

Facts gathered from the repo that drive the design:

- **`dataset.json`** — 5 tickets, uniform keys: `id` (GUID string), `name`, `email`, `description`, `summary`, `imageUrl`, `status`, `resolution`, `createdAt`, `updatedAt` (ISO-8601 Z).
- **Statuses in the data:** `New`, `In Progress`, `Closed`, `Resolved`. Note `In Progress` **contains a space** — a plain C# enum will not round-trip it, so a custom `JsonConverter` is required (see Stage 2). This is the single most likely source of silent data corruption in the exercise.
- **`imageUrl`** values look like `uploads/laptop_issue.jpg` — relative paths, implying a served static folder.
- **`new-ticket.png`** shows a **"Upload an image / Choose File"** field that the README's prose field list (Full Name, Email, Issue Description) omits. The dataset field + mockup outrank the prose; image upload is in scope.
- **`ticket-manage.png`** shows the admin table with columns Name / Summary / Status (dropdown) / Resolution (textarea), an "All" status dropdown, a search box, and a single **Save** button — i.e. inline batch editing.
- **`login.png`** shows email + password and a "Need an account? Register" link. The README only requires *login*, so Register will be rendered as a disabled/absent affordance rather than a half-built flow.
- **`MX_EX.slnx`** is `<Solution />` — the newer XML solution format, which needs SDK 9.0.200+.

---

## Target architecture

Four-project backend enforcing a strict **inward dependency rule** (`Api → Infrastructure → Application → Domain`; Domain depends on nothing):

```
MX_EX.slnx
├─ backend/
│  ├─ MX.Domain/           entities, value objects, enums, domain events — zero external deps
│  ├─ MX.Application/      DTOs, service interfaces + implementations, abstraction ports, validation
│  ├─ MX.Infrastructure/   JSON repository, email senders, AI providers, JWT, file storage
│  └─ MX.Api/              Minimal API endpoints, DI composition root, middleware
├─ tests/
│  ├─ MX.Application.Tests/  unit tests, all ports mocked
│  └─ MX.Api.Tests/          integration tests via WebApplicationFactory
└─ frontend/                 Vite + React + TS
```

Why this shape: the README explicitly asks for "**Entities, DTOs, and Service Classes**". Separate projects make the separation *compiler-enforced* rather than merely conventional — `MX.Domain` physically cannot reference ASP.NET, so domain logic can't leak into HTTP concerns.

### Patterns used, and the justification for each

| Pattern | Where | Why it earns its place |
|---|---|---|
| **Repository** | `ITicketRepository` / `JsonTicketRepository` | Isolates the JSON-file mechanics so services are testable without touching disk. |
| **Strategy** | `IEmailSender` → `MockEmailSender` \| `SmtpEmailSender`; `ISummaryGenerator` → `StubSummaryGenerator` \| `ClaudeSummaryGenerator` | Swap implementation by config with no call-site change — this is exactly what "generic integration with email service" asks for. |
| **Decorator** | `ResilientSummaryGenerator` wrapping any `ISummaryGenerator` | Timeout + fallback-to-empty without the provider knowing. A failed AI call must never block ticket creation. |
| **Domain Events + Observer** | `TicketCreated` / `StatusChanged` / `ResolutionChanged` → `EmailNotificationHandler` | Keeps `TicketService` ignorant of email. The README's three email triggers become three handlers, not three `if` blocks in the service. **This is the cleanest answer to the "3 email cases" requirement.** |
| **Options** | `JwtOptions`, `EmailOptions`, `AiOptions`, `StorageOptions` | Typed, validated config; no magic strings. |
| **Result** | `Result<T>` returned by services | Expected failures (not-found, validation) are values, not exceptions — endpoints map them to status codes in one place. |
| **Specification / query object** | `TicketQuery { Status?, Search? }` | Filtering lives in one testable unit instead of being smeared across the endpoint. |
| **DTO + explicit mapping** | `TicketDto`, `CreateTicketRequest`, `UpdateTicketRequest` | Entities never cross the HTTP boundary; mapping is hand-written and explicit (no AutoMapper indirection to explain). |

**SOLID in practice:** SRP — persistence, notification, and summarization are three collaborators, not three methods on a god-service. OCP — a new email channel is a new class plus one config value. LSP — every `IEmailSender` is total; the mock is a real implementation, not a throwing stub. ISP — `ISummaryGenerator` has one method. DIP — `MX.Application` declares the ports; `MX.Infrastructure` implements them and is referenced only by the composition root.

### API surface

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/auth/login` | anon | email+password → JWT |
| `GET` | `/api/tickets?status=&search=` | anon | filtered list |
| `GET` | `/api/tickets/{id}` | anon | single ticket (the `/tickets/{id}` deep link) |
| `POST` | `/api/tickets` | anon | create (multipart: fields + optional image) |
| `PUT` | `/api/tickets/{id}` | **admin** | update status and/or resolution |
| `GET` | `/uploads/{file}` | anon | static image serving |

Anonymous create + admin-only edit is exactly the README's bonus rule ("only logged users can edit tickets, all users can add new tickets").

---

## Stages

Each stage ends in a working, committed, test-green state.

### Stage 0 — Prerequisites
`dotnet` is **not installed** on this machine (verified; Node 26.3.0 is present). Install the .NET 9 SDK via `brew install --cask dotnet-sdk`, confirm `dotnet --list-sdks` reports 9.0.200 or newer (required by the `.slnx` format).
*Verify:* `dotnet --info` succeeds.

### Stage 1 — Solution skeleton
Create the four backend projects + two test projects, wire project references to enforce the dependency rule, add them to `MX_EX.slnx`. Add `Directory.Build.props` enabling `Nullable` and `TreatWarningsAsErrors`. Move `dataset.json` to `backend/MX.Api/Data/dataset.json` (copied to output, and the working copy the app writes to), keeping the original committed as the seed.
*Verify:* `dotnet build` clean; `dotnet test` runs zero tests successfully.

### Stage 2 — Domain + persistence
- `Ticket` entity with private setters and behavior methods (`ChangeStatus`, `SetResolution`, `AttachImage`) that stamp `UpdatedAt` and raise domain events — an entity, not an anemic bag of properties.
- `TicketStatus` enum + **`TicketStatusJsonConverter`** handling the `"In Progress"` space, with round-trip tests over the real `dataset.json`.
- `JsonTicketRepository`: loads once into an in-memory list, serves reads from memory, and on mutation serializes the whole list under a `SemaphoreSlim` and writes **atomically** (temp file + `File.Move` overwrite) so a crash mid-write can't truncate the dataset.
*Verify:* unit tests for converter round-trip, concurrent-write safety, and that seeded IDs load intact.

### Stage 3 — Application layer
`TicketService` implementing `ITicketService`: list (applies `TicketQuery` — status equality + case-insensitive substring match on name *or* description, per README), get-by-id, create (validate → generate summary → persist → raise `TicketCreated`), update (diff old vs new to raise `StatusChanged` / `ResolutionChanged` **only on actual change**). DTOs, hand-written mappers, `Result<T>`, and a validator for name/email/description.
*Verify:* unit tests with all ports mocked (NSubstitute) — filter combinations, no-op update raises no event, changed status raises exactly one event, invalid email rejected, summary failure still creates the ticket.

### Stage 4 — Minimal API
Endpoints grouped via `app.MapGroup("/api/tickets")` in a `TicketEndpoints` extension class (keeps `Program.cs` a readable composition root rather than a 200-line script). Global exception handler + `ProblemDetails`, `Result<T>` → HTTP status mapping, CORS for the Vite dev origin, Swagger in development.
*Verify:* integration tests via `WebApplicationFactory` against a **temp copy** of the dataset — full create/read/filter/update round-trip, 404 on unknown ID, 400 on invalid payload.

### Stage 5 — Auth
`IJwtTokenService` / `JwtTokenService` (HMAC-signed, role claim), `IUserService` with an admin credential from config, **password stored as a hash** (never plaintext in `appsettings`), `AddAuthentication().AddJwtBearer()`, `.RequireAuthorization("Admin")` on `PUT`.
*Verify:* integration tests — login returns a token; `PUT` without a token → 401; with a non-admin token → 403; with an admin token → 200.

### Stage 6 — Email notifications
`IEmailSender` with `MockEmailSender` (logs + keeps an in-memory `SentEmails` list that tests assert against) and `SmtpEmailSender` (MailKit, Gmail app password from user-secrets). Selected by `Email:Provider` config; **mock is the default**. `EmailNotificationHandler` subscribes to the three domain events; the create email includes the tracking link `{FrontendBaseUrl}/tickets/{id}` the README asks for.
*Verify:* integration test asserts exactly one email captured per create, per status change, and per resolution change — and **zero** on a no-op save.

### Stage 7 — AI summary
`ISummaryGenerator` with `StubSummaryGenerator` (deterministic first-sentence truncation — keeps tests fast and offline) and `ClaudeSummaryGenerator` (`claude-haiku-4-5-20251001` via the Messages API, key from user-secrets), wrapped in `ResilientSummaryGenerator` (timeout + swallow-and-fallback). Config-selected, **stub is the default**.
*Verify:* unit test proving a throwing/hanging generator still yields a created ticket with an empty summary.

### Stage 8 — Image upload
`IFileStorage` / `LocalFileStorage` writing to `wwwroot/uploads` with a GUID-based filename, content-type and size validation (images only, capped), returning the relative `uploads/{file}` path that matches the dataset convention. `app.UseStaticFiles()`.
*Verify:* integration test posts a multipart ticket with a small image, asserts `imageUrl` is populated and `GET /uploads/{file}` returns 200; a non-image upload is rejected.

### Stage 9 — Frontend foundation
Vite + React + TS scaffold in `frontend/`. `src/types/ticket.ts` mirroring the backend DTOs, a typed `apiClient` wrapper (base URL from `.env`, attaches the JWT), `AuthContext` holding the token in memory + `localStorage`, `react-router-dom` routes, and a `useTickets` hook owning fetch/filter state. Shared CSS variables for the palette drawn from the mockups.
*Verify:* `npm run build` clean; dev server lists real tickets from the running API.

### Stage 10 — Frontend screens
- **Tickets view** (`/`) — table, status dropdown + debounced search, full description and AI summary, row click → detail, "New Ticket" button.
- **New Ticket modal** — name, email, description, image picker; posts multipart; shows the simulated-email confirmation.
- **Detail view** (`/tickets/{id}`) — ID, customer details, description, summary, image, status dropdown and resolution textarea (**editable only when authenticated**, read-only otherwise), Save.
- **Login** (`/login`) — matching `login.png`.
Loading/error/empty states throughout; responsive down to mobile.
*Verify:* manual walkthrough of the three README screens against a running backend.

### Stage 11 — Documentation & handoff
Rewrite `README.md` with setup/run instructions for both halves, an architecture overview, the config matrix (how to flip mock→real for email and AI), a documented API table, and fresh screenshots of the actual UI. Add `.gitignore` entries for `node_modules`, `wwwroot/uploads`, and user-secrets.
*Verify:* clone-to-running follow-through from the README alone.

---

## Verification

**Backend:** `dotnet test` from the repo root — unit tests (fast, fully mocked) plus integration tests exercising real HTTP against a temp dataset copy. Target: every service method and every endpoint covered, including the auth failure paths and the "no email on no-op" case.

**Frontend:** `npm run build` must be clean (TypeScript catches DTO drift against the backend contract).

**End-to-end manual pass**, the flow a grader will actually run:
1. Start API (`dotnet run --project backend/MX.Api`) and frontend (`npm run dev`).
2. Create a ticket with an image as an anonymous user → appears in the table with an AI summary → console shows the simulated email with a `/tickets/{id}` tracking link.
3. Open that link directly in a fresh tab → detail view loads by ID; status and resolution are read-only.
4. Log in as admin → same page now editable → change status → save → console shows a second email.
5. Edit resolution → save → third email. Save again with no changes → **no** email.
6. Filter by status and by free text; confirm the results and that `dataset.json` on disk reflects every change.

---

## Risks

- **`"In Progress"` enum round-trip** — the highest-probability silent bug in this exercise. Pinned by a Stage 2 test that parses and re-serializes the shipped `dataset.json` byte-for-byte.
- **Concurrent writes to a single JSON file** — mitigated by the semaphore + atomic replace; called out in the README as a documented trade-off versus a real database.
- **Scope** — all four bonuses is a lot of surface. Stages 1–4 alone constitute a complete, submittable exercise; bonuses land in dependency order after that, so work is shippable at every stage boundary.
