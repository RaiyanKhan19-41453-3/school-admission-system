# AI Usage Disclosure

This project was built with AI assistance (Claude in OpenCode harnesses) for
scaffolding, boilerplate, and review. All design decisions, API contract
matching, concurrency reasoning, and test coverage were reviewed, corrected,
and verified by the author.

## What AI was used for

- Generating the initial file layout (Dockerfiles, `.csproj`, Vite config).
- Drafting EF-free Npgsql transaction code and explaining `FOR UPDATE` row locks.
- Drafting React polling/countdown code and Vitest stubs.
- Catching spec mismatches (status names, idempotency, `409` vs `400`).

## Where AI erred and how it was caught

| AI suggestion / mistake | Correction |
|---|---|
| First draft used `is null or < 1` for nullable int validation, which does not compile cleanly. | Rewrote as plain `== null \|\| < 1`. |
| Suggested a partial unique index to enforce one-hold-per-student, which would block re-applying after expiry because `HELD` rows need time-based comparison. | Removed it; enforce duplicate check under the class-row lock instead. |
| Suggested storing `expires_at` from app time. | Changed to `now() + interval` in SQL and compared with `now()` to avoid clock skew. |
| Initially drafted a single `Program.cs` only; missing test project wiring (`WebApplicationFactory<Program>` needs a visible `Program` type). | Added `public partial class Program` and a Testcontainers fixture. |
| Frontend draft counted down from server `expiresAt` but updated only on poll. | Added a local 1-second ticker so expiry shows without reload. |

## What the author verified manually

- Read the Bengali brief and re-checked every endpoint/field name against the implementation.
- Ran an end-to-end build (`docker compose up --build`) and walked the UI.
- Ran the concurrency test (`30 parallel holds → exactly 5 created`).
- Confirmed `docker compose down` + `up` preserves data via the `db_data` volume.
