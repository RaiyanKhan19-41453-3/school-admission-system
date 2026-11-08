# School Admission System

First-come-first-served school admission with time-limited seat holds.

## Quick start (Docker only)

```bash
# from the repo root — no local .NET / Node / Postgres needed
docker compose up --build
```

- Frontend (UI): http://localhost:5173
- Backend API: http://localhost:8080 — health: http://localhost:8080/health

> If port 8080/5173 is already in use on your machine, override:
> `BACKEND_PORT=18080 FRONTEND_PORT=15173 docker compose up --build`
> and open the frontend on the port you chose. The frontend still
> calls the backend via `VITE_API_URL` (see compose args).

`docker compose down` keeps data (named volume `db_data`), so
restarting does not lose classes or holds. `docker compose down -v`
wipes everything.

## How to use

1. Open the UI, enter a 17-digit birth certificate number at the
   top and click **Set student** (saved in `localStorage`).
2. **Create a class** (admin) with a name + total seats.
3. The list shows total / held / confirmed / available seats and
   refreshes every 3 seconds.
4. Click **Apply** on a class → seat is **held** for
   `HOLD_DURATION_SECONDS` (default 600).
5. **My applications** shows status, countdown to expiry,
   **Pay admission fee** (confirm) and **Cancel**.
   - If the window ends, status shows `EXPIRED` automatically
     (no reload needed) and the pay button is disabled.
   - `Pay admission fee` simulates a successful payment (no gateway).
6. Refreshing/closing the browser keeps your applications because
   they are fetched from `/me/holds` using the stored student ID.

## Environment variables

Backend:
- `DATABASE_URL` e.g. `postgres://postgres:postgres@db:5432/admissions`
  (or `PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD`)
- `HOLD_DURATION_SECONDS` (default `600`)
- `BACKEND_PORT` (compose host port, default `8080`)

Frontend (build arg):
- `VITE_API_URL` — where the API lives from the browser's point of
  view, default `http://localhost:8080`

## Run tests

Backend (needs Docker for Testcontainers):

```bash
cd backend
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test tests/Admissions.Tests.csproj
```

Or with an external Postgres:

```bash
DATABASE_URL=postgres://postgres:postgres@localhost:5432/admissions dotnet test tests/Admissions.Tests.csproj
```

Frontend:

```bash
cd frontend && npm install && npm test
```

## API (brief contract)

| Method | Path | Header | Success |
|---|---|---|---|
| POST | `/classes` | — | 201 |
| GET | `/classes`, `/classes/{id}` | — | 200 |
| POST | `/classes/{id}/holds` | `X-Student-Id` | 201 |
| GET | `/holds/{id}` | — | 200 |
| POST | `/holds/{id}/confirm` | `X-Student-Id` | 200 |
| DELETE | `/holds/{id}` | `X-Student-Id` | 204 |
| GET | `/me/holds` | `X-Student-Id` | 200 |

Statuses: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`. Errors are
`409` for no seats/duplicate/expired, `400` for bad input,
`403` for another student's hold, `404` when not found.

## Project layout

```
backend/            ASP.NET Core API + xUnit tests
frontend/           React + Vite UI + Vitest
docker-compose.yml  db + backend + frontend
```
