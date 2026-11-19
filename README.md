# School Admission System

First-come-first-served school admission with time-limited seat holds.
Layered ASP.NET Core + EF Core backend, React frontend, all Dockerized.

## Quick start

```bash
docker compose up --build
```

- Frontend: http://localhost:5173
- Backend: http://localhost:8080 (health: `/health`, OpenAPI: `/openapi/v1.json`)

No local .NET / Node / Postgres needed — only Docker.

## Features

- **Academic years** — admins create a year and activate it to open admissions
- **Classes / shifts** — each class+shift has its own seats and fee amount
- **Parent accounts** — register/login with NID (JWT auth), or use the legacy `X-Student-Id` header
- **Apply** → seat held for `HOLD_DURATION_SECONDS` (default 600)
- **Pay** → record payment (method + transaction ref) → admission confirmed
- **Cancel** → frees the seat; expired holds auto-expire via background sweeper
- **My applications** with countdown, fee, payment ref
- **Admin dashboard** — seat stats, application counts, revenue
- **Admin APIs** — manage years, classes, view all applications, student list

## Architecture

```
backend/
  Models/         AcademicYear, SchoolClass, Student, Hold, Payment, roles/status constants
  Data/           AdmissionDbContext + EF migrations
  Dtos/           request/response records
  Repositories/   IRepositories + EF implementations
  Services/       AdmissionService, AuthService, ExpiredHoldSweeper, domain exceptions
  Controllers/    Classes, Holds, Me (legacy contract) + Auth, Admin, Applications
  Middleware/     ExceptionHandlingMiddleware
  Program.cs      DI, JWT, CORS, OpenAPI, migrate+seed on startup
```

Concurrency: hold creation uses `SELECT ... FOR UPDATE` on the class row inside a
transaction, so `held + confirmed` never exceeds `totalSeats`, even across
multiple backend processes.

## Default accounts

- Admin: NID `ADMIN00000000000`, password `Admin@123` (seeded on first run)
- Parents register themselves in the UI

## Environment variables

- `ConnectionStrings__DefaultConnection` — Postgres connection (compose sets it)
- `Jwt__Key` — signing key (override with `JWT_KEY` env in compose)
- `HOLD_DURATION_SECONDS` — hold window in seconds (default 600)
- `VITE_API_URL` — frontend's backend URL build arg

## Tests

Backend: 5 xUnit integration tests (includes 30-parallel concurrency test).
Frontend: 2 Vitest tests. Docker-only commands:

```bash
# backend
docker run -d --name t -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=admissions -p 55439:5432 postgres:16-alpine && sleep 6
docker run --rm -v "$PWD/backend":/src --network host -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'DATABASE_URL=postgres://postgres:postgres@localhost:55439/admissions dotnet test tests/Admissions.Tests.csproj'
docker rm -f t

# frontend
docker run --rm -v "$PWD/frontend":/app -w /app node:22-alpine \
  sh -c 'npm ci --silent && npm test -- --run'
```
