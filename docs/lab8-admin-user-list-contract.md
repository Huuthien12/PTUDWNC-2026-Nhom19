# Lab 8 — Admin User List contract

## Decision

The user list is an **Admin-only** feature. There is no separate Author user-list requirement in SRS v1.0.2 or the Lab 8 assignment, so Authors do not receive user-list visibility. Any future Author directory must be approved as a separate scope and define its public fields and privacy rules first.

## API

- `GET /api/v1/admin/users`
- JWT authentication is required. Anonymous requests receive `401`; authenticated non-Admin requests receive `403`.
- Query parameters: `page` (default `1`), `pageSize` (default `12`, range `1–100`), `search` (up to 100 characters), and `role` (`Admin` or `Author`, case-insensitive).
- Response user fields are allowlisted: `id`, `fullName`, `email`, `userName`, `roles`, `emailConfirmed`, and `createdAt`.
- Password hashes, security stamps, refresh/access tokens, and other internal account data are excluded.

## UI

- `/admin/users` is protected by the frontend `RequireAuth` gate for `Admin`.
- The API remains the authoritative authorization boundary; the UI gate does not replace the API's `401`/`403` enforcement.
- The page supports loading, empty, retryable error, search, role filter, and pagination states.

## Verification

- Backend: `backend/CulinaryBlog.Tests/AdminUserApiTests.cs` covers role enforcement, pagination, search, role filter, invalid query parameters, DTO allowlisting, and query count.
- Frontend: `frontend/tests/lab6-h04.test.tsx` covers Admin rendering, Author denial before the list request, loading/retry/error, empty state, and search/filter/pagination query values.

## Welcome email job

- Registration enqueues one Hangfire welcome-email job only after the account and refresh-token session are created successfully.
- Jobs are deduplicated by normalized email using a Hangfire distributed lock and an atomic state association.
- An enqueued, processing, scheduled, awaiting, or succeeded job is not queued again. A terminal `Failed` or `Deleted` job may be recovered.
- `WelcomeEmailJob.SendAsync` keeps Hangfire's three-attempt automatic retry policy; retries reuse the same job and do not create a second active job.
- `backend/CulinaryBlog.Tests/HangfireWelcomeEmailQueueTests.cs` covers duplicate suppression, terminal recovery, and retry configuration.
