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
three random test accounts (including one administrator) and verifies cookie authentication, missing/stale CSRF,
owner-only reads/list/cancellations, forged user IDs, management-role denial,
concurrent idempotent retries, payload mismatch, independent competing bookings,
and logout. It also invokes the role-provisioning command, verifies admin booking
visibility/cancellation, normal-account denial, CSRF, provider/service creation and
working-hours validation/update/removal. Run from the repository root with the
same database configuration/environment as the API; the CLI must target that test
database. .NET must be available on PATH (or at the standard macOS install path).
Tests use synthetic `example.com` addresses and never print passwords
or cookie values.

Successful runs cancel their bookings. Accounts and cancelled history remain;
if a test fails after a write, inspect that test booking before retrying. CI uses
an isolated disposable PostgreSQL service, never Neon credentials or User Secrets.

For this portfolio verification, all migrations, repeated demo seeding and these
checks also passed against a fresh temporary PostgreSQL database that was removed
after the test, preserving the original application data.

## Price snapshot checks

The CI job also runs a .NET console-based integration check:

```sh
BOOKING_TEST_API=http://localhost:5130/api dotnet run --project tests/BookingSystem.PriceSnapshotChecks
```

It requires `ConnectionStrings__BookingDatabase` explicitly in the environment;
it does not load User Secrets. Use the same disposable database as the running API.
The database must be named `booking_ci` or start with `booking_verify_`. The check
verifies `current_database()` before writes and confirms its unique fixture is
visible through the API before creating an account or booking. Never point these
checks at application data. Database naming checks are an accidental-use guard,
not a replacement for separate credentials and permissions.

It verifies that a forged client price is ignored, changing a service price from
60 to 80.25 PLN preserves existing bookings and idempotent replays, new bookings
use the updated price, legacy NULL prices remain unknown, customer history
returns the snapshot, and PostgreSQL rejects zero/negative stored prices.
Successful checks cancel their bookings; dispose of the test database afterwards.
