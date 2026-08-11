# Local Development

Version: 2.4 (Sprint 3.6 checkout and orders)

## Prerequisites

- .NET 8 SDK (`8.0.423` or a newer supported .NET 8 SDK)
- Node.js `22.12` or newer and npm (`.nvmrc` selects Node 22)
- Docker Desktop with Docker Compose

Verify the tools:

```powershell
dotnet --info
node --version
npm --version
docker compose version
```

## Environment configuration

```powershell
Copy-Item .env.example .env
Copy-Item frontend/.env.example frontend/.env
```

The checked-in values are development examples, not production secrets. At
minimum, deployment must supply unique values for:

- `Jwt__SigningKey`
- `Otp__HashKey`
- `AdministratorSeed__Username`
- `AdministratorSeed__Password`
- `ConnectionStrings__DefaultConnection`
- `MediaStorage__RootPath` (a writable persistent path)

Checkout configuration is validated at startup. Its defaults are:

```text
Checkout__Currency=TJS
Checkout__TimeZoneId=Asia/Dushanbe
Checkout__OpeningTime=10:00
Checkout__ClosingTime=22:00
Checkout__SchedulingWindowHours=4
Checkout__PickupIntervalMinutes=15
```

The scheduling window and interval are intentionally fixed by the current API
contract. A deployment/configuration update to business hours is observed by
checkout validation without changing its HTTP contract; no staff settings UI
exists in Sprint 3.6.

Real Telegram mode additionally requires:

- `Telegram__Enabled=true`
- `Telegram__BotToken`
- `Telegram__BotUsername`
- `Telegram__WebhookSecret`
- `Telegram__PublicBaseUrl` (public HTTPS backend scheme and host)
- `Telegram__WebhookPath=/api/v1/telegram/webhook`
- `Telegram__RegisterWebhookOnStartup=true`
- `Telegram__UseDevelopmentSender=false`

The JWT and OTP keys must each contain at least 32 characters. Allowed CORS
origins are comma- or semicolon-separated and may not contain `*`.

## Local startup

Start PostgreSQL:

```powershell
docker compose up -d postgres
```

Restore tools, apply migrations, and run the backend:

```powershell
dotnet tool restore
dotnet ef database update --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development
dotnet run --project backend/MoodPickup.Api
```

Run the frontend in another terminal:

```powershell
Set-Location frontend
npm install
npm run dev
```

## Full Docker startup

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up --build
```

Compose waits for PostgreSQL, applies pending migrations in `Development`,
seeds the configured first administrator, seeds the neutral demo menu when the
menu database is empty, then starts the frontend after backend health succeeds.
Automatic migration startup is rejected outside Development.

Default example administrator credentials:

```text
admin / ChangeThisDev1!
```

Change them in `.env`.

The demo menu is not the official Mood menu. It contains five categories, five
products, and reusable Size, Milk, and Syrups options. Seeding is idempotent,
never overwrites an existing menu, and exits outside `Development`.

Compose mounts the named `moodpickup-media` volume at `/app/uploads`. The
backend image is prepared so its non-root application user can write there.
`docker compose down` preserves both database and media volumes; do not add
`--volumes` unless deletion is deliberate.

Stop containers while preserving the database:

```powershell
docker compose down
```

## Development OTP flow

1. Open <http://localhost:5173/login>.
2. Enter a normalized number such as `+992900000000`.
3. Read the generated OTP from `docker compose logs backend`.
4. Enter the code at `/verify`.
5. Enter a name at `/register` for a new customer.

Only the Development fake sender logs OTPs. The API never returns them.
The fake sender is impossible outside `Development`.

## Real Telegram testing through an HTTPS tunnel

No tunnel provider is built into the application. To test a real bot before
production:

1. Start PostgreSQL, backend, and frontend.
2. Expose backend port 8080 through a public HTTPS tunnel.
3. Set `Telegram__PublicBaseUrl` to that tunnel's scheme and host only.
4. Set `Telegram__Enabled=true`,
   `Telegram__UseDevelopmentSender=false`, and
   `Telegram__RegisterWebhookOnStartup=true`.
5. Supply the bot token, username without `@`, and a random webhook secret
   containing only letters, digits, `_`, or `-`.
6. Restart the backend. Safe startup logs show `getMe` validation, webhook URL,
   and pending update count without secrets.
7. Open `/login`, request a code, open the returned bot link, press Start, and
   share the contact using Telegram's button.
8. Use startup logs/readiness as the application diagnostic for
   `getWebhookInfo`; no token-bearing manual curl command is required.

BotFather command descriptions may be configured as:

```text
/start - Начать подтверждение входа
/help - Помощь
```

## Authentication transport

- Access token: frontend memory only.
- Refresh token: Secure, HttpOnly, `SameSite=Lax` cookie restricted to
  `/api/v1/auth`.
- CSRF token: readable Secure cookie at `/`; refresh and logout copy the value
  into `X-CSRF-TOKEN`.
- Browser storage: never used for access, refresh, or registration tokens.

The anonymous customer cart is the deliberate non-sensitive exception to
general browser-storage avoidance. It uses only `moodpickup.cart.v1` in
`localStorage`; it never contains authentication/CSRF values, identity,
administrative metadata, media storage keys, or physical paths. Clear it from
browser developer tools when a fresh cart fixture is needed.

Secure cookies on loopback rely on the browser’s localhost secure-context
exception. Use HTTPS for non-local deployments.

## Entity Framework Core

```powershell
# List migrations
dotnet ef migrations list --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development

# Apply migrations
dotnet ef database update --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development

# Add a future migration
dotnet ef migrations add MigrationName --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api --output-dir Data/Migrations -- --environment Development

# Generate an idempotent deployment script
dotnet ef migrations script --idempotent --project backend/MoodPickup.Api --startup-project backend/MoodPickup.Api -- --environment Development
```

Current migrations:

- `20260805090000_InitialFoundation`
- `20260806062531_Sprint2AuthenticationSecurity`
- `20260806072221_Sprint3MenuDomain`
- `20260806081107_Sprint3MenuApiAudit`
- `20260806125941_RealTelegramAuthentication`
- `20260807142646_Sprint36CheckoutOrders`

Production migrations are an explicit release step.

### Verify the development menu seed

With the Compose stack running:

```powershell
docker exec moodpickup-postgres psql --username moodpickup --dbname moodpickup --command 'SELECT COUNT(*) FROM "Categories";'
docker exec moodpickup-postgres psql --username moodpickup --dbname moodpickup --command 'SELECT COUNT(*) FROM "Products";'
docker exec moodpickup-postgres psql --username moodpickup --dbname moodpickup --command 'SELECT COUNT(*) FROM "OptionGroups";'
```

The neutral seed returns 5 categories, 5 products, and 3 option groups on an
empty development database. Restart the backend and repeat the queries to
confirm the counts do not change. If any menu data already exists, the seeder
intentionally does nothing.

### PostgreSQL API integration tests

Critical Sprint 3.2-3.4 menu and media API tests use a disposable real
PostgreSQL database and an isolated temporary media directory.
Start one example instance:

```powershell
docker run --name moodpickup-sprint32-tests --detach `
  --env POSTGRES_DB=moodpickup_api_tests `
  --env POSTGRES_USER=moodpickup `
  --env POSTGRES_PASSWORD=moodpickup_test `
  --publish 55433:5432 postgres:16-alpine

$env:MOODPICKUP_POSTGRES_TEST_CONNECTION = `
  'Host=127.0.0.1;Port=55433;Database=moodpickup_api_tests;Username=moodpickup;Password=moodpickup_test;Pooling=false'
dotnet test MoodPickup.sln --no-build --no-restore
```

The PostgreSQL test fixture drops/recreates only the database named by that
connection and applies the full EF migration chain. Without the variable,
the PostgreSQL-specific tests are reported as skipped while fast tests still
run. Do not point this variable at a database containing data to preserve.

## URLs

| Purpose | URL |
| --- | --- |
| Customer menu | <http://localhost:5173> |
| Product details | `http://localhost:5173/product/{id}` |
| Local cart | <http://localhost:5173/cart> |
| Checkout (customer) | <http://localhost:5173/checkout> |
| Order success (customer) | `http://localhost:5173/order-success/{id}` |
| Customer login | <http://localhost:5173/login> |
| Staff login | <http://localhost:5173/staff/login> |
| Staff menu | <http://localhost:5173/staff/menu> |
| Categories | <http://localhost:5173/staff/menu/categories> |
| Products | <http://localhost:5173/staff/menu/products> |
| Option groups | <http://localhost:5173/staff/menu/option-groups> |
| Audit log (Administrator) | <http://localhost:5173/staff/audit-log> |
| Health page | <http://localhost:5173/health> |
| Swagger | <http://localhost:8080/swagger> |
| Live health | <http://localhost:8080/health/live> |
| Ready health | <http://localhost:8080/health/ready> |
| System information | <http://localhost:8080/api/v1/system/info> |
| Public categories | <http://localhost:8080/api/v1/categories> |
| Public products | <http://localhost:8080/api/v1/products> |
| Uploaded media | <http://localhost:8080/media/{storageKey}> |
| SignalR hub | <http://localhost:8080/hubs/notifications> |

## Media configuration

Development defaults are:

```text
MediaStorage__Provider=Local
MediaStorage__RootPath=uploads
MediaStorage__PublicBasePath=/media
MediaStorage__MaximumFileSizeBytes=5242880
MediaStorage__MaximumImageWidth=6000
MediaStorage__MaximumImageHeight=6000
MediaStorage__MaximumDecodedImageBytes=268435456
```

`AllowedContentTypes` defaults to `image/jpeg`, `image/png`, and `image/webp`.
Array entries may be overridden with
`MediaStorage__AllowedContentTypes__0`, `__1`, and so on. Configuration is
validated during startup. Production must supply an appropriate writable
persistent root; no operating-system-specific path is compiled into the API.

The server fully decodes and normalizes accepted files. SVG, GIF, empty,
oversized, MIME-mismatched, multi-frame, malformed, over-dimension, and
over-decoded-byte inputs are rejected.

## Validation

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
docker compose up --build -d
docker compose ps
```

For direct menu smoke checks, sign in through `/api/v1/staff/auth/login`, copy
the returned access token into Swagger authorization, and use the GUID
`rowVersion` returned by every admin resource for updates/deletes. Refresh
tokens remain cookie-only and are unrelated to menu concurrency.

### Manual Sprint 3.3 verification

1. Sign in as `MenuManager`; confirm Menu is visible and Audit Log is absent.
2. Sign in as `Administrator`; confirm Menu and Audit Log are visible.
3. Create/edit/reorder/hide/delete/restore a category.
4. Create a product, upload a JPEG/PNG/WebP, assign it, and open its `/media/`
   URL.
5. Restart the backend and confirm the image is still retrievable.
6. Filter/reorder/toggle/duplicate/delete/restore products.
7. Create an option group/value, assign selected values to a product, configure
   modifier/default/measurements, and observe accepted draft warnings.
8. Submit a stale row version and confirm reload/discard conflict actions.
9. Confirm the administrator audit list/detail shows the changes and JSON is
   rendered as text.
10. Confirm a customer cannot open menu-admin pages or call the media upload
    endpoint, and a MenuManager receives `403` from the audit API.

### Manual Sprint 3.4 verification

1. Open `/` without signing in and confirm categories and grouped products load.
2. Search by product name and description; confirm the URL and empty/clear
   states update after the debounce.
3. Select category chips, use browser back/forward, and open a copied deep link.
4. Open a product and confirm description, ingredients, metrics, option
   defaults, unavailable values, and modifiers are display-only.
5. Confirm unavailable/non-orderable products remain visible with backend
   warning messages.
6. Verify card and detail image placeholders by assigning no image or using a
   deliberately broken test response.
7. Repeat at desktop, tablet, and 390 px viewport widths with keyboard
   navigation and visible focus.
8. Use browser network throttling to confirm skeletons do not shift layout and
   retry recovers from a failed menu request.
9. Confirm the browser console has no errors or warnings.
10. Restart the backend and verify previously uploaded product media remains
    retrievable.

### Manual Sprint 3.5 verification

1. Open `/` anonymously, open Cappuccino, and confirm current defaults.
2. Change Size, select Syrups, reach the maximum, and observe configured price
   and measurement changes.
3. Add the same configuration twice and confirm one line with quantity two.
4. Add a different configuration and confirm a separate line.
5. Use quantity, edit, remove, clear, and continue-browsing controls.
6. Refresh `/cart`; confirm the cart restores and revalidates without blocking
   menu rendering.
7. In developer tools set `moodpickup.cart.v1` to malformed JSON, refresh, and
   confirm a non-blocking recovery notice and usable empty cart.
8. Exercise a stale fixture (removed product/value or changed price) and confirm
   the line remains visible as Updated, Needs attention, or Unavailable.
9. Repeat at 1440x900, 768x1024, and 390x844 with keyboard-only controls and
   confirm there is no page-level horizontal overflow.
10. Confirm localStorage contains only the whitelisted cart schema and no
    access, refresh, CSRF, customer, staff, storage-key, or physical-path data.
11. Confirm the Checkout action opens `/checkout` for a signed-in customer and
    redirects an anonymous visitor to customer sign-in.
12. Confirm a fresh production tab has no console errors or warnings.

### Manual Sprint 3.6 verification

1. Sign in as a customer through the Development Telegram fake sender, then add
   a current configurable item to the local cart.
2. Open `/checkout`; confirm the name/phone are not requested again, every
   selected option and total is shown, and the default is Prepare ASAP / Pay on
   pickup.
3. Select Scheduled pickup. Verify an empty time is blocked in the browser;
   then try a past, tomorrow, non-15-minute, outside-hours, and more-than-four-
   hours time. Each must return a validation error without clearing the cart.
4. Submit a valid checkout. Confirm `/order-success/{id}` shows the `MP-...`
   number, total, pickup mode, payment method, and `PendingConfirmation`.
5. Confirm `moodpickup.cart.v1` is removed and Redux's cart badge is zero only
   after the successful response.
6. In Swagger, use the customer token to call `GET /api/v1/orders/mine` and
   `GET /api/v1/orders/{id}`. A second customer's token must receive `404` for
   the first customer's ID.
7. Change a product name/price or option modifier as an administrator after
   checkout. Confirm the existing order response still returns its original
   snapshots.
8. Inspect PostgreSQL: `Orders`, `OrderItems`, and `OrderItemOptions` contain
   the created data; `OrderDailySequences` increments once for the café date.

## Troubleshooting

### Backend exits during configuration validation

Check that the JWT and OTP keys are at least 32 characters, allowed origins are
configured, the password denylist is non-empty, and enabled administrator seed
credentials satisfy the password policy.

### Customer code returns `TELEGRAM_NOT_CONFIGURED`

Neither the Development fake sender nor enabled real Telegram mode is active.
For local fake delivery, use `Development` with
`Telegram__UseDevelopmentSender=true`. For real delivery, supply all Telegram
settings and enable the integration.

### Backend exits during Telegram startup

In real mode startup intentionally fails if `getMe` fails, the configured
username does not match, registration fails, or `getWebhookInfo` reports a
different URL. Confirm the public base URL is HTTPS and contains no path, the
webhook path is `/api/v1/telegram/webhook`, and the tunnel/reverse proxy routes
that path to the backend. Never paste the token into logs or issue history.

### Refresh or logout returns `CSRF_VALIDATION_FAILED`

Confirm requests use `credentials: include`, the readable CSRF cookie exists,
and its exact value is sent in `X-CSRF-TOKEN`. Cookie names and frontend
build-time configuration must match.

### `/health/live` works but `/health/ready` returns 503

The API is alive but PostgreSQL cannot be reached, or real Telegram startup
state is unhealthy. Check database health, credentials, and
`ConnectionStrings__DefaultConnection`; then inspect safe webhook registration
logs. Health probes themselves do not call Telegram.

### CORS failure

Add the exact frontend origin to `AllowedOrigins`. Credentials are enabled, so
wildcard origins are rejected.

### Node audit reports React Router findings

See `Technical_Debt.md`. Do not run `npm audit fix --force`.
