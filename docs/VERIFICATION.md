# Verification record

Local checks completed on 2026-10-17:

- .NET solution build and 25 xUnit cases passed (no failures).
- React/TypeScript production build and ESLint passed.
- Full migration chain applied to an empty PostgreSQL database.
- Demo seeding ran twice without duplicating the sample data.
- API checks passed for two accounts, cookies, CSRF, ownership, role denial,
  idempotent retries, payload mismatch, independent booking races and logout.
- Temporary verification databases are removed after testing.
- React provider/service/date/slot flow checked against the API in browser.
- Screenshots captured from the actual application, without customer credentials.
- Git publication candidates checked for common credential patterns; build outputs,
  dependencies and IDE directories are ignored. This is a scoped heuristic check,
  not a guarantee that every possible secret format is detectable.

GitHub Actions CI passed on 2026-10-17 for the published application commit `cec2fc97c4c3ccba35c0f6c190f32eb0b129ec73`.

[Verified CI run](https://github.com/erpolIT/BookingSystem/actions/runs/39037037157) includes backend tests, frontend build/lint, fresh PostgreSQL migrations, demo seeding and API integration checks.
This record covers local verification before the first GitHub publication.

English interface update: frontend build, ESLint and .NET build passed. Browser checks confirmed English labels, PLN prices and Europe/Warsaw appointment times; documentation screenshots refreshed. Native browser date-picker controls follow the browser language.

## Administrator update — 2026-10-18

- Existing-account role provisioning is repeatable and requires a new sign-in.
- Public registration cannot grant administrator access through extra fields.
- Normal accounts receive 403 and anonymous requests 401 on administrator routes.
- Administrator writes require CSRF tokens; customer routes retain ownership checks.
- Provider/service creation, working-hours upsert/removal, date/provider booking
  filters, and repeatable administrative cancellation passed integration checks.
- Schedule changes preserve existing appointments and share the booking write lock.
- .NET tests (25), TypeScript production build and ESLint passed.
- An initial schema-isolation attempt did not isolate writes and created synthetic
  fixtures in the development database. The synthetic administrator/provider were
  removed. Tests were repeated successfully in a separate temporary database,
  checking current_database() before writes; that database was removed afterwards.
  Earlier claims of schema isolation are superseded by this database-level check.

## Booking price snapshot — 2026-11-02

- .NET tests (25), TypeScript production build and ESLint passed.
- EF reported no pending model changes after generating the migration.
- Migrated a disposable database from AddIdentityAccounts with an existing
  booking: the record was retained and PriceAtBooking remained NULL.
- Identity/administrator API integration checks passed, including prices in the
  administrator list and ignoring client-supplied booking prices.
- Real PostgreSQL/API price checks passed: catalogue changes preserve historical
  prices and replay results; subsequent bookings use the new decimal price;
  legacy unknown prices stay NULL; zero/negative database prices are rejected.
- The database name was verified before writes and an API fixture marker checked
  before API writes. The disposable database was removed after verification.
