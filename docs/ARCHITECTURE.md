# Architecture and decisions

The React client calls `/api` through Vite's development proxy. ASP.NET Core
controllers validate HTTP input and enforce authorization. EF Core handles
persistence in PostgreSQL. `AvailabilityService` loads data, while `SlotGenerator`
and `BookingTimeRules` evaluate scheduling without database dependencies.

## Booking invariants

- A service belongs to one provider, with its own price and duration.
- Starts are spaced 15 minutes from the provider's opening time.
- The entire visit must fit in the provider's daily hours and start in the future.
- Availability is offered up to one year ahead, in Europe/Warsaw local dates.
- Confirmed bookings for a provider cannot overlap. The PostgreSQL GiST exclusion
  constraint compares half-open UTC ranges, allowing adjacent visits.
- Cancelled bookings remain in history and no longer block availability.
- The server derives the provider, owner and end time; the request cannot assign them.

## Concurrent requests and retries

A booking is stored in a transaction. A transaction-scoped PostgreSQL advisory
lock serializes the same idempotency key; a unique account/key index protects
against duplicates. Matching retries return the original booking resource (200),
including its current cancelled status if it has subsequently been cancelled.
Different payloads with the same account/key return 409. Independent requests for
the same provider/time are protected by the exclusion constraint, returning 409.
A resource replay is intentional; this API does not replay an immutable byte-for-byte
snapshot of the original HTTP response.

The client keeps an unresolved payload and UUID in per-account sessionStorage.
A timeout preserves both for retry. A confirmed conflict clears the selected slot,
refreshes availability and preserves the contact fields. Closing the tab may remove
sessionStorage; a client can then inspect its own bookings after logging in.

## Authentication and scope

See [backend authentication](../backend/AUTHENTICATION.md). Read-only discovery
is public; booking access is owner-only. Management writes require an Administrator
role, with no public role-granting endpoint. Old unowned bookings remain unclaimed.
The scope has no administrator UI, email delivery, payment processing or public deployment.

Schedules use one interval per weekday. Concurrent administrative schedule changes
are not serialized with booking creation; the current portfolio flow uses seeded
schedules, without a live schedule-editing UI. Availability is an advisory view,
while the exclusion constraint is the final check against double booking.
