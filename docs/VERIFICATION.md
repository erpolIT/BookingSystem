# Verification record

Local checks completed on 2026-10-17:

- .NET solution build and 25 xUnit cases passed (no failures).
- React/TypeScript production build and ESLint passed.
- Full migration chain applied to an empty, isolated PostgreSQL schema.
- Demo seeding ran twice without duplicating the sample data.
- API checks passed for two accounts, cookies, CSRF, ownership, role denial,
  idempotent retries, payload mismatch, independent booking races and logout.
- The isolated verification schema was removed after testing.
- React provider/service/date/slot flow checked against the API in browser.
- Screenshots captured from the actual application, without customer credentials.
- Git publication candidates checked for common credential patterns; build outputs,
  dependencies and IDE directories are ignored. This is a scoped heuristic check,
  not a guarantee that every possible secret format is detectable.

The GitHub Actions workflow is prepared, but has not yet run on GitHub.
This record covers local verification before the first GitHub publication.

English interface update: frontend build, ESLint and .NET build passed. Browser checks confirmed English labels, PLN prices and Europe/Warsaw appointment times; documentation screenshots refreshed. Native browser date-picker controls follow the browser language.
