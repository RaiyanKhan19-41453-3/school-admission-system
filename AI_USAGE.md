# AI Usage Disclosure

This project was built with AI assistance (Claude in the OpenCode harness) for
scaffolding, boilerplate, implementation drafts, and review. All design decisions,
API contract matching, concurrency reasoning, license checks, and verification were
reviewed and corrected by the author.

## What AI was used for

- Initial layout: Dockerfiles, `.csproj`, Vite config, React Router structure.
- Drafting Npgsql transaction code and explaining `FOR UPDATE` row locks.
- EF Core layered architecture: Models / Data / Dtos / Repositories / Services / Controllers / Middleware.
- JWT auth, academic years, classes with fees, payments, application form, document uploads.
- Receipt PDF and CSV exporters (researched PDF library licenses, then hand-wrote both).
- Mantine UI pages, notifications bell, admin dashboard charts.
- GitHub Actions CI workflow and integration tests.
- Catching spec mismatches and runtime bugs during browser verification.

## Where AI erred and how it was caught

| AI mistake | Correction |
|---|---|
| Used `is null or < 1` for nullable int validation (does not compile cleanly). | Rewrote as `== null || < 1`. |
| Suggested a partial unique index for one-hold-per-student; it cannot express “HELD and not expired”. | Removed; duplicate check lives under the class-row lock. |
| Used app time (`DateTime.UtcNow`) for `expires_at`. | Restored DB `now() + make_interval(...)` and DB-clock comparisons in the sweeper. |
| Top-level statements made `Program` inaccessible to `WebApplicationFactory`. | Added `public partial class Program`. |
| Countdown updated only on poll. | Added a local 1-second ticker. |
| Invented a “rule 7: never 500” from faulty `pdftotext` extraction of the Bengali PDF. | Read the visual PDF: no such rule. Removed the claim from ASSUMPTIONS.md. |
| Login form stripped letters from the admin ID (`ADMIN...` → `000...`). | Allowed letters in the login field. |
| Redirect loop after login (profile loaded only at mount). | Lifted token/profile into App state; profile re-fetch on token change; wait for `/me` before navigating. |
| Bell shown to admins → 403 every 10s. | Render the bell only for Parent role. |
| Several missing component imports (`Select`, `ThemeIcon`) crashed the UI. | Caught via browser console; added imports. |
| Evaluated PDF libraries: iText = AGPL, QuestPDF = community license, PDFsharp 7 = AGPL, PDFsharp 6.2.4 pulls advisories. | Hand-wrote a minimal PDF 1.4 writer — zero dependencies, zero license risk. |
| README test command lacked the Docker socket for Testcontainers. | Verified correct commands with a throwaway Postgres + `DATABASE_URL`. |
| Healthcheck used `wget` (absent in aspnet image). | Switched to a bash `/dev/tcp` check. |

## How it was verified

- Read the **visual** PDF (rendered pages) for the real contract and rules after text extraction proved unreliable.
- `docker compose up --build` end-to-end; walked every page in a real browser with console capture.
- Backend build: 0 errors / 0 warnings; 5 original + 8 feature integration tests.
- Frontend build + 2 Vitest tests; GitHub Actions CI runs both on every push.
- Black-box evaluator simulation: 52/52 on the original contract; 100,000-request load test proved no overbooking.
- License research: every dependency is MIT/open source; PDF/CSV code is own work.
- Containers hardened to run as non-root; data persists via named volumes.

## Principle followed

AI drafts, the author verifies. No code was accepted that could not be explained,
and every claim (licenses, rules, test results, bugs) was checked against primary
sources or live behaviour.
