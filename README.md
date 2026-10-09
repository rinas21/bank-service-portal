# Bank Service Portal

A service-request management portal for internal bank staff. Staff submit and
track requests, support agents work the queue, managers approve and audit, and
administrators manage users, branches, and legacy data migration.

Stack: React + TypeScript SPA, ASP.NET Core 8 API, EF Core on SQL Server, and
Docker Compose for the whole stack.

## Features

**Requests**
- Create, edit, and search requests with filters for status, priority,
  category, branch, and approval requirement, plus sorting and pagination.
- Lifecycle `Open → InProgress → Resolved → Closed`, with a reason captured on
  each change.
- Assignment and reassignment with notes, a public/internal comment thread,
  and an approval workflow.

**Administration**
- User management with role assignment (Admin, Manager, Support, Employee) and
  deactivate/reactivate instead of hard delete.
- Branch management with per-branch user and open-request counts.
- Audit log of sign-ins and domain changes, filterable by action and date.

**Migration**
- CSV import of legacy requests with validate-then-write semantics, duplicate
  detection, per-row reporting, and a standalone CLI for one-off loads.

**Platform**
- JWT auth with role-based authorization, rate-limited sign-in, and consistent
  JSON error envelopes.
- Health endpoints (`/health/live`, `/health/ready`) and OpenAPI/Swagger in
  development.
- Responsive UI down to 390 px, keyboard-navigable dialogs, and ARIA labels.

## Architecture

```
BankService.Domain          entities, enums, domain rules
BankService.Application     DTOs, interfaces, service contracts
BankService.Infrastructure  EF Core, Identity, services, seeding, migrations
BankService.Api             controllers, middleware, health checks, Swagger
BankService.Tests           unit + integration tests
BankService.CsvImporter     standalone CLI for legacy imports
frontend                    React 19 + TypeScript + Vite + Tailwind CSS 4
database/csv                sample import files
docs                        migration and operations notes
```

Controllers depend on Application interfaces, Infrastructure implements them,
and `Program.cs` wires everything together. Request permission rules live in a
`RequestActor` value derived from the caller's role plus their relationship to
the request, so they are not scattered through controllers.

## Quick start

Prerequisites: Docker with Compose v2. The .NET and Node toolchains are only
needed to run things outside containers.

```bash
git clone <repository-url>
cd bank-service-portal

cp .env.example .env
# Edit .env and set MSSQL_SA_PASSWORD and Jwt__Key.
# Generate a key with: openssl rand -base64 64 | tr -d '\n'

docker compose up --build -d
```

Then open:

| Service | URL |
| --- | --- |
| Portal | http://localhost:8080 |
| API | http://localhost:8081 |
| Swagger (dev) | http://localhost:8081/swagger |
| API health | http://localhost:8081/health/ready |

Startup is ordered by health checks: the API waits for SQL Server, and the
frontend waits for the API to report ready. The API applies pending EF
migrations and seeds demo data on first boot, so there is no separate
migration step.

To start over from an empty database:

```bash
docker compose down -v
docker compose up --build -d
```

## Demo accounts

All seeded accounts use the password `Password@123`.

| Email | Role | Sees |
| --- | --- | --- |
| `admin@bankportal.com` | Admin | Everything, plus user/branch admin and CSV migration |
| `manager@bankportal.com` | Manager | All requests, approvals, branches, audit log |
| `support1@bankportal.com` | Support | Requests, comments, and status changes |
| `employee1@bankportal.com` | Employee | Their own requests |

Seeded content: 5 branches, 7 users, and 35 requests across every status,
priority, and category.

## Configuration

Every setting is read from environment variables, so nothing sensitive is
committed. `.env.example` lists the full set:

| Variable | Purpose |
| --- | --- |
| `MSSQL_SA_PASSWORD` | SQL Server `sa` password; must meet the complexity policy |
| `SQL_DATABASE` | Database name created on first boot (default `BankServicePortal`) |
| `Jwt__Key` | HMAC signing key, at least 64 characters |
| `Jwt__Issuer` / `Jwt__Audience` | Token issuer and audience |
| `Jwt__ExpiryMinutes` | Access token lifetime in minutes |
| `ASPNETCORE_ENVIRONMENT` | `Production` by default in Compose |
| `SWAGGER_ENABLED` | Expose Swagger outside development |
| `API_PORT` / `FRONTEND_PORT` / `SQL_PORT` | Host port mappings |

Production deliberately has no fallback JWT key: the API refuses to start if
`Jwt__Key` is missing, rather than signing tokens with a known secret.

## Local development without Docker

```bash
# API on https://localhost:8081
dotnet run --project BankService.Api

# Frontend on http://localhost:5173, proxying /api to the API above
cd frontend
npm ci
npm run dev
```

Set `ConnectionStrings__DefaultConnection` and `Jwt__Key` in your shell or
`BankService.Api/appsettings.Development.json`. The Vite dev server proxies
`/api` and `/health` so the browser stays same-origin, matching the nginx proxy
used in Docker.

## Tests

```bash
dotnet test BankService.sln
```

The suite covers authentication and the JSON error contract, role-based
authorization for every request action, the CSV importer's validation and
duplicate handling, request-number sequencing, internal-comment visibility,
paging bounds, and user role filtering.

Frontend checks:

```bash
cd frontend
npm run lint
npm run build   # tsc -b then vite build
```

## CSV migration

Both the in-app importer (`POST /api/migration/import`, Admin only) and the
standalone CLI share the same validation logic. See
[docs/csv-migration.md](docs/csv-migration.md) for the column format, sample
files, and troubleshooting. Sample data lives in `database/csv/`.

## Security notes

- Secrets live only in `.env` (git-ignored) or your deployment platform's
  secret store; `.env.example` contains placeholders.
- Passwords are hashed by ASP.NET Core Identity with default strong settings.
- JWT signing keys must be supplied explicitly in production.
- Sign-in is rate limited to 10 attempts per minute, and all other API traffic
  to 600, each partitioned per client IP. Behind a reverse proxy the app reads
  `X-Forwarded-For` and trusts only the right-most entry, so a client cannot
  spoof its way past the limit. Set `ForwardedHeaders__TrustKnownProxies=true`
  with `ForwardedHeaders__KnownProxies__0` to restrict this to named proxies.
- Error responses never leak stack traces; a trace ID is returned instead.
- The API container runs as a non-root user.
