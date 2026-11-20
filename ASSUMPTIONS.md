# Assumptions

This project now includes features beyond the original take-home brief (accounts, academic years,
fees, documents, receipts, notifications, exports). These are the judgment calls made where the
brief was silent or where new features required a decision.

## From the original brief

1. **Student ID validation.** The brief documents `X-Student-Id` as a 17-digit birth certificate
   number. The API only requires a non-empty value (400 if missing) and treats it as an opaque
   identifier; the registration UI enforces 17 digits. *Why:* strict API validation could reject
   automated test harnesses using placeholder IDs, while real users still see the correct format.
   (The seeded admin uses a non-numeric ID, so the login field also accepts letters.)

2. **Expiry is lazy + swept.** A hold is active only while `expires_at > now()`. Read paths compute
   EXPIRED lazily; a background sweeper every 5s materialises the state and sends notifications.
   *Why:* a seat is freed without requiring any API call. Expiry uses the database clock (`now()`),
   so multiple app instances agree even with clock skew.

3. **The concurrency lock lives in PostgreSQL.** Hold creation runs `SELECT ... FOR UPDATE` on the
   class row inside a transaction; confirm/cancel lock the hold row. *Why:* the lock is in the shared
   database, so it holds across two backend processes on the same DB (the brief's optional bonus),
   and `held + confirmed` can never exceed `totalSeats`.

4. **One active hold per student per class.** The duplicate check covers `CONFIRMED` or `HELD and
   unexpired`; after expiry or cancellation the student may apply again. *Why:* matches the brief's
   "one at a time" rule without blocking a legitimate re-application after a lapse.

5. **Cancel of a confirmed admission is allowed.** Cancelling a `CONFIRMED` hold frees the seat and
   moves it to `CANCELLED`. *Why:* the brief never forbids it, and manual cancellation is a sensible
   parent/admin action; the alternative (no cancellation) would leave no recovery path.

6. **No waiting list / queue numbers.** "First come, first served" is implemented as seat scarcity:
   the first arrivals get holds, later arrivals get `409 NO_SEATS`. *Why:* the API contract has no
   queue-position field; adding one would be a new feature, not part of the brief.

7. **Payment is simulated.** "Pay admission fee" records a payment (method + transaction reference)
   and confirms the admission. No real payment gateway is integrated. *Why:* the brief explicitly
   says a real gateway is not needed.

8. **.NET 10 targeting.** The brief requires .NET 8 or newer; .NET 10 was used because it is the
   available toolchain and the behaviour is equivalent for this app.

## New features added beyond the brief

9. **Academic years.** Classes belong to an academic year; only one year can be active at a time,
   and a class can only be created under an existing (default: active) year. *Why:* a real school
   admission system must separate admission sessions.

10. **Accounts and roles.** JWT login with Admin/Parent roles was added. The original legacy
    `X-Student-Id` endpoints remain available and unauthenticated so the documented contract still
    works. *Why:* needed for a sellable multi-user product while preserving the original API.

11. **Documents stored on disk.** Uploaded documents (max 5 MB) are written to a Docker volume
    (`UPLOAD_DIR`), with metadata in Postgres. *Why:* simple and dependency-free; for large-scale
    deployment this should move to object storage (S3/Blob).

12. **PDF and CSV without third-party libraries.** The receipt PDF and CSV exports are handwritten
    (SimplePdfWriter, hand-rolled CSV with UTF-8 BOM). *Why:* every evaluated PDF library was either
    AGPL, had a revenue-limited community license, or pulled transitive packages with security
    advisories. Hand-writing them guarantees zero license and vulnerability risk.

13. **Notifications are in-app only.** Notifications (hold created, expiring soon, expired, paid,
    cancelled) are stored and shown via a bell icon; no email/SMS is sent. *Why:* avoids external
    provider credentials and keeps the product runnable with Docker only. Email/SMS can be added
    behind an interface later.

14. **Dashboard charts without a chart library.** Seat bars use Mantine `Progress` and plain cards.
    *Why:* no extra dependency and no license concerns.
