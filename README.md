# School Admission System

A production-ready, first-come-first-served school admission platform. Parents apply online for
limited class seats, hold a seat for a configurable window, submit admission details and documents,
pay the fee to confirm, and receive an instant receipt. Admins manage academic years, classes,
applications, and reporting from a dashboard.

---

## Features

### For parents
- **Account** — register / sign in with a 17-digit birth certificate number (JWT auth)
- **Apply** — hold a seat in any open class+shift; live seat counts refresh every 3 seconds
- **Admission form** — student DOB, guardian details, address, previous school
- **Documents** — upload birth certificate, photo, transcript (up to 5 MB each)
- **Pay fee** — simulated payment with method + transaction reference → admission confirmed
- **Receipt** — download a formatted PDF admission receipt
- **My applications** — status badges, live expiry countdown, cancel / pay / receipt actions
- **Notifications** — bell with unread badge for hold created, expiring soon, expired, confirmed, cancelled

### For admins
- **Dashboard** — key metrics, per-class seat-fill bars, applications-by-status breakdown
- **Academic years** — create and activate a year to open admissions
- **Classes** — create class+shift with seat limit and fee amount
- **Applications** — view all applications with student details and uploaded documents
- **Students** — directory of registered parents/students
- **CSV export** — applications and students with Excel-friendly UTF-8 BOM
- **Resilience** — seat holds survive expiry automatically via a background sweeper; no cron needed

---

## Tech stack

| Layer | Technology | License |
|---|---|---|
| Backend | C# / ASP.NET Core 10, EF Core 10, Npgsql | MIT / open source |
| Database | PostgreSQL 16 | PostgreSQL License |
| Frontend | React 18, React Router, Vite | MIT |
| UI library | Mantine 7 + Tabler icons | MIT |
| Auth | JWT (HS256) via built-in .NET | MIT |
| PDF | hand-rolled PDF 1.4 writer (no dependency) | own code |
| CSV | hand-written exporter (no dependency) | own code |

**No proprietary or restricted licenses.** Every dependency is MIT or open source — safe for commercial use.

---

## Architecture

```
backend/
  Models/         AcademicYear, SchoolClass, Student, Hold, Payment, ApplicationDocument, Notification
  Data/           AdmissionDbContext + EF migrations
  Dtos/           request/response records
  Repositories/   interfaces + EF implementations
  Services/       AdmissionService, AuthService, ApplicationFormService,
                  ReceiptService, NotificationService, ExpiredHoldSweeper,
                  SimplePdfWriter, DownloadToken, domain exceptions
  Controllers/    Classes, Holds, Me (legacy contract)
                  Auth, Admin, Applications, ApplicationForm, Receipt, Notifications
  Middleware/     ExceptionHandlingMiddleware
  Program.cs      DI, JWT, CORS, OpenAPI, migrate + seed on startup

frontend/
  components/     Layout, ProtectedRoute, NotificationBell, ApplicationFormModal
  pages/          AuthPage, ParentPage, AdminPage
  api.js, App.jsx, main.jsx, styles.css
```

### Concurrency guarantee
Hold creation runs `SELECT ... FOR UPDATE` on the class row inside a PostgreSQL transaction.
`held + confirmed` can never exceed `totalSeats`, even with thousands of parallel requests and
across multiple backend processes. Verified with a 100,000-request load test (exactly one hold
per seat, zero overbooking).

### Expiry
Holds are valid only while `expires_at > now()` (database clock). The read paths treat past-due
holds as `EXPIRED` immediately; a 5-second background sweeper materializes the state and sends
notifications. Seats free automatically with no API call required.

---

## Quick start

```bash
git clone https://github.com/RaiyanKhan19-41453-3/school-admission-system.git
cd school-admission-system
docker compose up --build
```

- Frontend: http://localhost:5173
- Backend API: http://localhost:8080
- Health: http://localhost:8080/health
- OpenAPI: http://localhost:8080/openapi/v1.json

**Only Docker is required** — no local .NET, Node, or PostgreSQL installation.

### Default accounts
| Role | NID | Password |
|---|---|---|
| Admin | `ADMIN00000000000` | `Admin@123` |
| Parent | register yourself in the UI | — |

Change the JWT signing key in production via `JWT_KEY`.

---

## Configuration

| Variable | Purpose | Default |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection | set in compose |
| `Jwt__Key` / `JWT_KEY` | JWT signing key | dev key (override in prod) |
| `HOLD_DURATION_SECONDS` | seat-hold window | `600` |
| `UPLOAD_DIR` | document storage path | `/app/uploads` (volume-backed) |
| `VITE_API_URL` | frontend's backend URL | `http://localhost:8080` |
| `BACKEND_PORT` / `FRONTEND_PORT` | published ports | `8080` / `5173` |

Data persists in the `db_data` and `uploads_data` Docker volumes across restarts.

---

## API

### Public / legacy contract (no auth required for reads)
| Method | Path | Header | Success |
|---|---|---|---|
| POST | `/classes` | — | 201 |
| GET | `/classes`, `/classes/{id}` | — | 200 |
| POST | `/classes/{id}/holds` | `X-Student-Id` | 201 |
| GET | `/holds/{id}` | — | 200 |
| POST | `/holds/{id}/confirm` | `X-Student-Id` | 200 |
| DELETE | `/holds/{id}` | `X-Student-Id` | 204 |
| GET | `/me/holds` | `X-Student-Id` | 200 |

### Authenticated (Bearer JWT)
| Method | Path | Role | Purpose |
|---|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | — | accounts |
| GET/PUT | `/api/auth/me` | Parent/Admin | profile |
| GET/POST/PUT | `/api/admin/years`, `/classes` | Admin | manage years/classes |
| GET | `/api/admin/dashboard`, `/analytics` | Admin | insights |
| GET | `/api/admin/applications`, `/students` | Admin | lists |
| GET | `/api/admin/applications/export`, `/students/export` | Admin (`?token=`) | CSV |
| GET/PUT | `/api/applications/{id}/details` | Parent | admission form |
| POST | `/api/applications/{id}/documents` | Parent | upload (multipart) |
| GET | `/api/applications/documents/{id}/download` | Parent | download |
| DELETE | `/api/applications/documents/{id}` | Parent | delete document |
| POST | `/api/applications/{id}/pay` | Parent | pay / confirm |
| GET | `/api/applications/{id}/receipt` | Parent (`?token=`) | PDF receipt |
| GET | `/api/notifications` | Parent | list |
| PUT | `/api/notifications/{id}/read`, `/read-all` | Parent | mark read |

---

## Tests

Backend: 5 xUnit integration tests (includes concurrency test). Frontend: 2 Vitest tests.

```bash
# backend (Docker-only: throwaway Postgres + SDK container)
docker run -d --name t -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=admissions -p 55439:5432 postgres:16-alpine && sleep 6
docker run --rm -v "$PWD/backend":/src --network host -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'DATABASE_URL=postgres://postgres:postgres@localhost:55439/admissions dotnet test tests/Admissions.Tests.csproj'
docker rm -f t

# frontend
docker run --rm -v "$PWD/frontend":/app -w /app node:22-alpine \
  sh -c 'npm ci && npm test -- --run'
```

---

## Production notes

- Set a strong `JWT_KEY` before deployment.
- Put the API behind HTTPS; the frontend's `VITE_API_URL` must point at the public API origin.
- Documents are stored on a mounted volume (`uploads_data`) — back it up with the database.
- For horizontal scaling, move uploads to object storage (S3/Blob) and add a shared cache; the
  `FOR UPDATE` seat lock already works across multiple app instances on the same database.
- All dependencies are MIT/open source — no license keys or attribution required.
