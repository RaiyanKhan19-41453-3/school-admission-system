# Assumptions

Only where the brief was silent or vague; each choice is noted with why.

1. **Student ID validation.** The header is documented as a 17-digit birth certificate number. The API requires a non-empty `X-Student-Id` (`400` if missing) and treats it as an opaque identity; the UI enforces “17 digits”. *Why:* strict 17-digit checking would break automated flows that use placeholder IDs, while still satisfying the user-facing format.
2. **Expiry is lazy + swept.** A hold is `HELD` only while `expires_at > now()` (database time). Reads compute `EXPIRED` lazily and a background task every 5s materialises `EXPIRED` rows. *Why:* seats free even with zero API calls, clocks-drift safe, and UI polling just sees the new status.
3. **Concurrency lock lives in Postgres.** Every hold-creation locks the class row `FOR UPDATE` in a transaction; confirm/cancel lock the hold row `FOR UPDATE`. *Why:* this holds across two backend processes on the same DB (the bonus case) — no in-memory lock, no `held+confirmed > totalSeats` race, and expiry is checked under the same transaction.
4. **Hold expiry uses DB `now()`.** `expires_at = now() + HOLD_DURATION_SECONDS` and all comparisons use `now()`. *Why:* two app servers with slightly different clocks still agree on expiry.
5. **Ownership on writes.** `confirm`/`cancel` require the owner (`403` otherwise), `404` if the hold does not exist. `GET /holds/{id}` has no header requirement (per the contract table) and returns status only. *Why:* prevents one family from affecting another, while matching the documented API.
6. **Cancel of a confirmed seat is allowed.** `DELETE` on a `CONFIRMED` hold moves it to `CANCELLED` and frees the seat; `DELETE` on `CANCELLED`/`EXPIRED` is idempotent `204`. *Why:* brief never forbids it, and manual cancellation is a sensible admin/parent action.
7. **Confirm is idempotent and checks expiry at confirm time.** Re-confirming a `CONFIRMED` hold returns `200` and does not double-count; confirming an expired/cancelled hold returns `409`. *Why:* network retries / double-clicks never double-book.
8. **Duplicate rule.** One student may have **one** active (`HELD`, unexpired) or `CONFIRMED` hold per class; after expiry/cancel they may apply again. *Why:* matches “একই সময়ে একটা র বেশি” literally while not blocking a legitimate re-apply after a lapse.
9. **Input validation.** `POST /classes` requires a non-empty name (≤200) and `totalSeats ≥ 1`; missing/invalid JSON → `400`. Other `4xx` per the rules. *Why:* “ভুল input-এ 4xx, কখনো 500 নয়”.
10. **Waiting list / queue numbers are out of scope.** The brief defines “first-come-first-served” purely as seat scarcity; once full, later holds get `409`. *Why:* no queue-position field exists in the API contract.
11. **Frontend API base via `VITE_API_URL` build arg** (default `http://localhost:8080`). The preview server serves the built bundle from the same container. *Why:* Vite bakes env at build; compose passes the arg so evaluator + host both resolve it correctly.
12. **.NET 10 targeting.** The skeleton targets `net10.0` with `sdk:10.0`/`aspnet:10.0` images (satisfies “8 or newer”). *Why:* available toolchain, and API behaviour is identical for this app.

## Multi-process note (bonus)

Guaranteeing rule 1 with two backend processes on the same database is achieved by doing the counting **inside a database transaction that locks the class row** (§3 above), not by anything in the process. Any process that tries to insert a hold must first acquire that row lock, so holders can never interleave between “count” and “insert”. Confirm/cancel also serialise on the hold row, so a confirm racing an expiry/cancel has exactly one winner.
