# Mood Pickup

Mood Pickup is a pickup-ordering platform for Mood Dushanbe. The current
foundation includes the Sprint 3.8 kitchen workflow and real
Telegram customer authentication:

- ASP.NET Core 8 Web API with PostgreSQL and EF Core;
- customer phone authentication with Telegram deep-link contact verification,
  webhook delivery, and a Development-only fake sender;
- employee username/password authentication;
- HS256 JWT access tokens and rotating HttpOnly refresh-token cookies;
- double-submit CSRF protection for refresh and logout;
- refresh-token family reuse detection and revocation;
- policy-based employee authorization;
- categories, products, reusable option groups and product-specific option
  assignments;
- provider-independent product image metadata plus validated JPEG/PNG/WebP
  upload and controller-mediated delivery;
- database and service validation for menu configuration and availability;
- soft deletion, UTC audit timestamps, and optimistic menu concurrency;
- anonymous category/product search and product-configuration endpoints;
- policy-protected category, product, global option, and product-assignment
  administration;
- progressive draft configuration with structured orderability issues;
- transactional employee menu audit logs and an administrator audit API;
- idempotent Development-only neutral menu seed data;
- React/Vite customer authentication plus a permission-aware staff layout,
  menu administration, draft/orderability guidance, visible concurrency
  handling, and administrator audit-log pages;
- a responsive customer menu with grouped category navigation, debounced
  server search, URL-synchronized category filters, product cards, safe image
  fallbacks, and interactive product configuration;
- a Redux Toolkit customer cart with canonical configuration merging,
  integer-minor-unit price calculations, quantity/edit/remove/clear actions,
  a global cart indicator, and versioned `moodpickup.cart.v1` persistence;
- bounded, deduplicated cart revalidation against cached public product
  details, with Current, Updated, Needs attention, and Unavailable guidance;
- customer-only validated checkout with configured pickup scheduling and
  payment-method selection;
- immutable PostgreSQL order, item, and selected-option snapshots with unique
  daily human-readable order numbers;
- customer order creation, owned-order retrieval, and newest-first paginated
  order summaries;
- employee order dashboards for Administrator, Cashier, and Manager roles with
  pending-order confirmation, required ready-time assignment, rejection, and
  optimistic concurrency;
- a responsive `/staff/kitchen` dashboard for active confirmed, preparing, and
  ready orders, with Kitchen/Administrator actions and view-only access for
  Cashier, Manager, and Pickup roles;
- sequential preparation, ready, pickup-payment, and completion transitions
  with immutable status history, employee attribution, audit snapshots, and
  GUID row-version concurrency;
- customer My Orders and live order tracking for confirmation, rejection,
  preparation, ETA, ready, payment, and completion updates through authenticated
  SignalR groups, with polling only as a disconnected fallback;
- backend authentication/domain/order tests, real-PostgreSQL menu/media/order
  API tests, and network-boundary frontend Vitest coverage;
- Docker Compose local orchestration with persistent PostgreSQL and media
  volumes.

Backend cart storage, online payment gateways, refunds, and persisted
multi-channel notifications remain outside the current scope. Online checkout
is treated as already paid; pay-on-pickup supports audited Cash/Card receipt.
The cart is an anonymous device-local draft and contains no trusted commercial
truth; checkout recalculates it from the database before persisting an order.

## Technology

- Backend: ASP.NET Core 8, EF Core 8, PostgreSQL, SignalR, FluentValidation,
  Serilog, API Versioning, Swagger
- Frontend: React 19, TypeScript 7, Vite 8, React Router, TanStack Query,
  Redux Toolkit, SignalR client, Vitest, React Testing Library
- Infrastructure: PostgreSQL 16 and Docker Compose

The repository remains on .NET 8 because Sprint 1 used the permitted .NET 8
fallback and Sprint 2 explicitly prohibits upgrading to .NET 9.

## Prerequisites

- .NET SDK 8.0.423 or a newer supported .NET 8 SDK
- Node.js 22.12 or newer; `.nvmrc` selects Node 22
- npm
- Docker Desktop with Docker Compose

## Full startup with Docker Compose

```powershell
Copy-Item  .env
docker compose up --build
```

Development Compose applies pending migrations, seeds the configured first
administrator, and adds the neutral demo menu only when the menu database is
empty. The example development credentials are:

```text
Username: admin
Password: ChangeThisDev1!
```

Change these values in `.env`. Production credentials and cryptographic keys
must always come from secret configuration.

The development menu contains Coffee, Tea, Cold Drinks, Breakfast, and
Desserts with a small configurable sample. It is not the official current Mood
menu. Restarting the backend does not duplicate it, and it is never seeded
outside `Development`.

Uploaded product media is stored in the named `moodpickup-media` volume at
`/app/uploads`. `docker compose down` preserves this volume. Only an explicit
volume deletion removes uploaded files.

Stop the stack while preserving PostgreSQL data:

```powershell
docker compose down
```

## Local development without full Docker

Start PostgreSQL:

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
```

Apply migrations and run the backend:

```powershell
dotnet tool restore
dotnet ef database update --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development
dotnet run --project backend/MoodPickup.Api
```

Run the frontend in another terminal:

```powershell
Set-Location frontend
Copy-Item .env.example .env
npm install
npm run dev
```

## Customer authentication

The default Compose profile uses the Development-only fake sender. Any valid
normalized international phone number may request an OTP, and the generated
code appears only in backend structured logs:

```powershell
docker compose logs backend
```

The API never returns the OTP. Real mode returns an opaque
`https://t.me/{bot}?start={token}` link for an unlinked phone. The bot accepts
only private-chat contact sharing owned by the same Telegram sender, compares
the normalized contact number to the website challenge, links the identity,
and then delivers the OTP. Existing linked customers receive the OTP directly.
Production forbids the fake sender.

Access tokens live only in frontend memory. Refresh tokens are stored only in a
Secure, HttpOnly cookie. Do not put either token in browser storage.

The anonymous cart is the only current application data written to
`localStorage`, under the versioned key `moodpickup.cart.v1`. Its product,
option, display, and price snapshots are untrusted previews. Authentication
tokens, CSRF values, customer identity, administrative data, media storage
keys, and physical paths are never included. Browser-storage failures leave
the in-memory cart usable and show a non-blocking warning.

## Service URLs

| Service | URL |
| --- | --- |
| Customer menu | <http://localhost:5173> |
| Product details | `http://localhost:5173/product/{id}` |
| Local cart | <http://localhost:5173/cart> |
| Checkout (customer) | <http://localhost:5173/checkout> |
| Order success (customer) | `http://localhost:5173/order-success/{id}` |
| My orders (customer) | <http://localhost:5173/orders> |
| Customer login | <http://localhost:5173/login> |
| Customer profile | <http://localhost:5173/profile> |
| Staff login | <http://localhost:5173/staff/login> |
| Staff dashboard | <http://localhost:5173/staff> |
| Staff orders (Administrator, Cashier, Manager) | <http://localhost:5173/staff/orders> |
| Kitchen dashboard (Kitchen, Cashier, Manager, Pickup, Administrator) | <http://localhost:5173/staff/kitchen> |
| Menu administration | <http://localhost:5173/staff/menu> |
| Categories | <http://localhost:5173/staff/menu/categories> |
| Products | <http://localhost:5173/staff/menu/products> |
| Option groups | <http://localhost:5173/staff/menu/option-groups> |
| Audit log (Administrator) | <http://localhost:5173/staff/audit-log> |
| Frontend health page | <http://localhost:5173/health> |
| Swagger | <http://localhost:8080/swagger> |
| Live health | <http://localhost:8080/health/live> |
| Ready health | <http://localhost:8080/health/ready> |
| System information | <http://localhost:8080/api/v1/system/info> |
| Public categories | <http://localhost:8080/api/v1/categories> |
| Public products | <http://localhost:8080/api/v1/products> |
| Uploaded media | <http://localhost:8080/media/{storageKey}> |
| SignalR hub | <http://localhost:8080/hubs/notifications> |
| PostgreSQL | `localhost:5432` |

## EF Core migrations

- `20260805090000_InitialFoundation`
- `20260806062531_Sprint2AuthenticationSecurity`
- `20260806072221_Sprint3MenuDomain`
- `20260806081107_Sprint3MenuApiAudit`
- `20260806125941_RealTelegramAuthentication`
- `20260807142646_Sprint36CheckoutOrders`
- `20260811061908_Sprint37StaffOrderManagement`
- `20260811073656_Sprint38KitchenWorkflow`

```powershell
dotnet ef migrations list --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development
dotnet ef database update --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development
```

Automatic migration application is permitted only in `Development`. Production
migrations remain an explicit release step.

## Build and test

```powershell
dotnet restore MoodPickup.sln
dotnet build MoodPickup.sln --no-restore
dotnet test MoodPickup.sln --no-build --no-restore
dotnet format --verify-no-changes

Set-Location frontend
npm install
npm run build
npm test -- --run
npm audit

Set-Location ..
docker compose config --quiet
```

Real PostgreSQL menu API tests run when
`MOODPICKUP_POSTGRES_TEST_CONNECTION` is set to a disposable PostgreSQL
database connection string. See the local-development guide for an example.

Detailed setup and troubleshooting are in
[`docs/04_Development/Local_Development.md`](docs/04_Development/Local_Development.md).
Current dependency and security follow-ups are recorded in
[`docs/04_Development/Technical_Debt.md`](docs/04_Development/Technical_Debt.md).
