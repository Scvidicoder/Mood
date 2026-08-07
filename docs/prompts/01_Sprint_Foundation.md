# Codex Prompt — Sprint 1: Foundation

## Role

Act as a senior full-stack engineer working inside the existing `MoodPickup` repository.

Your task is to create the technical foundation only. Do not implement business modules such as customer OTP, menu management, cart, orders, payments, kitchen workflow, or Telegram integration yet.

## Mandatory documentation

Read these files before changing the repository:

1. `README.md`
2. `docs/00_Project/00_Project_Overview.md`
3. `docs/03_Backend/10_Architecture.md`
4. `docs/04_Development/11_Deployment.md`
5. `docs/04_Development/13_Developer_Guide.md`
6. `docs/03_Backend/07_API.md`
7. `docs/03_Backend/08_Authentication.md`

Documentation is the source of truth. Do not replace the documented MVC-style folder architecture with Clean Architecture, MediatR, repository abstractions, or multiple backend class-library projects.

## Goal

Create a working monorepo foundation with:

- ASP.NET Core Web API backend;
- React + TypeScript + Vite frontend;
- PostgreSQL;
- Docker Compose;
- health checks;
- Swagger;
- global ProblemDetails error handling;
- Serilog structured logging;
- CORS configuration;
- API versioning;
- basic SignalR hub;
- frontend routing and API infrastructure;
- environment configuration;
- build and smoke-test instructions.

The entire repository must build successfully.

## Backend requirements

Create one backend application project under:

```text
backend/MoodPickup.Api/
```

Use the latest stable .NET SDK already available in the environment. Do not hard-code a preview target. If .NET 9 is unavailable, use .NET 8 and document the decision.

Required packages or equivalent official packages:

- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.EntityFrameworkCore.Design
- Swashbuckle.AspNetCore
- ASP.NET API versioning
- FluentValidation.AspNetCore or current supported FluentValidation integration
- Serilog.AspNetCore
- Serilog.Sinks.Console
- Microsoft.AspNetCore.SignalR

Enable:

- nullable reference types;
- implicit usings;
- XML documentation generation when practical.

Create clear MVC-style folders:

```text
Authorization/
Controllers/
Data/
DTOs/
Entities/
Extensions/
Hubs/
Infrastructure/
Interfaces/
Mappings/
Middleware/
Services/
Validators/
```

Do not add empty interfaces or fake abstractions merely to fill folders. Keep `.gitkeep` files where no real code exists yet.

### Backend behavior

Implement:

1. `Program.cs` registration using extension methods where this improves readability.
2. PostgreSQL `MoodPickupDbContext`.
3. A minimal initial entity only if needed for EF setup; do not invent domain entities.
4. `GET /health/live`
5. `GET /health/ready`
6. `GET /api/v1/system/info`
7. Swagger in Development.
8. Global exception handling returning RFC 7807 ProblemDetails.
9. Request logging with Serilog.
10. CORS from configured allowed origins.
11. API versioning under `/api/v1`.
12. SignalR hub at `/hubs/notifications`.
13. Configuration validation on startup.
14. A development-safe database migration strategy. Do not silently apply migrations in Production.

`GET /api/v1/system/info` should return non-secret information such as:

```json
{
  "service": "MoodPickup.Api",
  "environment": "Development",
  "apiVersion": "1.0",
  "utcTime": "..."
}
```

### Backend configuration

Provide:

- `appsettings.json`
- `appsettings.Development.json`
- documented environment variable overrides
- no committed secrets

Use connection string key:

```text
ConnectionStrings:DefaultConnection
```

## Frontend requirements

Create the frontend under:

```text
frontend/
```

Use:

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- Redux Toolkit
- `@microsoft/signalr`

Create folders:

```text
src/
├── api/
├── app/
├── assets/
├── components/
├── features/
├── hooks/
├── layouts/
├── pages/
├── store/
├── types/
└── utils/
```

Implement only foundation pages:

- `/` — project placeholder page
- `/staff` — staff placeholder page
- `/health` — frontend page that checks backend health and system info
- not-found page

Create:

- shared API client;
- QueryClient setup;
- Redux store setup;
- router;
- environment variable typing;
- SignalR connection factory without connecting to business groups;
- loading and error states.

Do not implement visual product design yet. Use simple, readable placeholder UI.

## Docker requirements

Update `docker-compose.yml` to run:

- PostgreSQL
- backend
- frontend

Add:

- backend Dockerfile
- frontend Dockerfile
- health checks
- dependency ordering based on health
- persistent PostgreSQL volume
- environment variable support

Development can expose:

- frontend: `5173`
- backend: `8080`
- PostgreSQL: `5432`

Do not add Nginx unless needed for this sprint. Production Nginx belongs to a later deployment sprint.

## Repository files

Create or update:

- `README.md`
- `.env.example`
- `.editorconfig`
- `.gitignore`
- `docker-compose.yml`
- backend and frontend Dockerfiles
- a short `docs/04_Development/Local_Development.md`

README must include:

- prerequisites;
- local run without Docker;
- Docker run;
- database migration commands;
- URLs;
- troubleshooting.

## Database migration

Create a valid initial EF Core migration for the foundation database.

The migration may create only framework or foundation-level structures that genuinely exist. Do not invent the final business schema during this sprint.

## Validation commands

Run all applicable commands and fix failures:

```bash
dotnet restore
dotnet build
dotnet test
npm install
npm run build
docker compose config
```

If Docker is available, also run a container smoke test. If it is unavailable, state that honestly in the completion report.

## Scope constraints

Do not implement:

- customer login;
- employee login;
- Telegram bot;
- menu;
- cart;
- orders;
- payments;
- notifications business logic;
- role policies beyond placeholders required for compilation;
- final branding;
- tests for modules that do not exist.

Do not:

- introduce Clean Architecture projects;
- add MediatR;
- add repository or Unit of Work abstractions over EF Core;
- add AutoMapper unless a real mapping need exists;
- add dependencies not required by this sprint;
- rename documented public API routes without explanation.

## Completion report

At the end, provide:

1. Summary of created files and architecture.
2. Exact commands executed.
3. Build/test results.
4. URLs for local services.
5. Any deviations from documentation and reasons.
6. Remaining work for Sprint 2.
7. A concise manual verification checklist.

Do not claim a command succeeded unless it was actually run successfully.
