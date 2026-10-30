# Lab 6 frontend foundation decision

## Authentication state

`frontend/src/services/auth-session.ts` remains the single client-side auth
store. `useSession` subscribes to it, while TanStack Query only caches server
data and is cleared when the session identity changes. No Zustand store is
introduced, so there is no second client auth source of truth.

## Refresh-token limitation

The current API contract returns the refresh token in the authentication
response, so the frontend persists the existing session object in
`localStorage`. This is an XSS exposure risk and is not changed by this PR.

Moving the refresh token to an HttpOnly, Secure, SameSite cookie requires a
coordinated backend API and CSRF-contract change. Until that decision is made,
frontend code must not log tokens, place them in URLs, or add another token
store.
