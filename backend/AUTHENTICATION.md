# Accounts and booking ownership

Accounts use ASP.NET Core Identity with the existing PostgreSQL context.
Passwords are hashed by Identity. Authentication uses a non-persistent HttpOnly,
SameSite=Strict cookie, expiring after eight hours. Production cookies require
HTTPS; local development supports the existing HTTP proxy.

## API

- GET /api/auth/csrf: request token; cookie is issued automatically.
- POST /api/auth/register: email and password, then login separately.
- POST /api/auth/login: email and password; successful login sets the auth cookie.
- GET /api/auth/me: current user or null.
- POST /api/auth/logout: terminates the browser session.
- GET /api/bookings: current user's bookings only.
- GET /api/bookings/{id}: owner's booking, otherwise 404.
- POST /api/bookings: authenticated owner is assigned by the server.
- POST /api/bookings/{id}/cancel: owner only, otherwise 404.

Every unsafe MVC request requires X-CSRF-TOKEN obtained from /api/auth/csrf.
Fetch a fresh token after login or logout, because it is tied to the identity.
Anonymous access to private routes returns 401, without HTML redirects.

Idempotency keys are scoped to the authenticated account. Client pending requests
are stored under a per-user sessionStorage key. A lost response must be retried
with the original payload and key after logging back into the same account.

Existing bookings have UserId = NULL. They remain in the database, continue to
block time if confirmed, and are not claimed automatically based on email.

Provider, service and working-hours mutations require the Administrator role.
No registration endpoint grants that role; an administrator provisioning flow
is outside this stage. Browsing providers, services and availability stays public.

## Current scope

Implemented: registration, login, logout, own-booking list, ownership enforcement,
CSRF protection, password policy, temporary lockout and scoped idempotency.
Email verification, password recovery and an administrator UI are not implemented.

## Verification

See tests/README.md. Integration checks cover two independent accounts, forged
user IDs, legacy bookings, missing/stale CSRF tokens, HttpOnly cookies,
concurrent retries, cross-account reads/cancellations, role denial and logout.
The React registration/login/booking/list/cancellation flow was also checked in
browser. Cancelled test bookings and random test accounts remain in the database.
