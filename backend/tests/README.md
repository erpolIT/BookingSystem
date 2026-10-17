# API integration checks

Run against a development database only. Restore/apply migrations and run
`dotnet run -- --seed-demo` from `backend` first, then start the API. The seeded
Adam provider and Haircut service use ID 1 on a new database. Existing data may
have other IDs; adjust the fixture selection before testing a populated database.

From the repository root:

```sh
BOOKING_TEST_API=http://localhost:5130/api python3 backend/tests/identity_integration.py
```

Python 3.9+ and only standard-library modules are required. The script creates
two random test accounts and verifies cookie authentication, missing/stale CSRF,
owner-only reads/list/cancellations, forged user IDs, management-role denial,
concurrent idempotent retries, payload mismatch, independent competing bookings,
and logout. Tests use synthetic `example.com` addresses and never print passwords
or cookie values.

Successful runs cancel their bookings. Accounts and cancelled history remain;
if a test fails after a write, inspect that test booking before retrying. CI uses
an isolated disposable PostgreSQL service, never Neon credentials or User Secrets.

For this portfolio verification, all migrations, repeated demo seeding and these
checks also passed against a fresh temporary PostgreSQL schema that was removed
after the test, preserving the original application data.
