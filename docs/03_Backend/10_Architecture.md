# Architecture

Version: 1.9 (Sprint 4.0 employee management)

## Goal

Mood Pickup remains a single ASP.NET Core Web API backed directly by EF Core
and PostgreSQL. Sprint 3.5 extends the public customer menu with an interactive
frontend-owned local cart while consuming the existing projected public API.
Sprint 3.6 adds focused order entities and checkout services to the same API.
Sprint 3.7 extends those entities and services with the staff order workflow;
it does not add a server cart or new abstraction layers.
Sprint 3.9 extends the same customer/order services with a profile cabinet,
owned history projections, and repeat-order validation; it does not introduce
a server cart, account aggregate, or new architectural layer.
Sprint 4.0 extends the same MVC application with focused employee-management
controllers/services, DTOs, validators, and one migration; it does not add
repositories, MediatR, a Unit of Work, or a separate identity project.
Real Telegram authentication remains inside the same API and adds only its
focused authentication migration.

## Repository

```text
backend/
frontend/
docs/
docker/
```

## Backend

- ASP.NET Core 8 Web API
- Entity Framework Core 8
- Npgsql 8 and PostgreSQL 16
- SignalR
- FluentValidation
- Serilog
- JWT authentication

The repository stays on .NET 8, matching the Sprint 1 fallback decision and
Sprint 2 implementation.

## Request flow

The intended application flow remains:

```text
Controller -> Service -> MoodPickupDbContext -> PostgreSQL
```

Public controllers call `IPublicMenuService`. Policy-protected admin
controllers call focused category, product, option, product-configuration, and
audit query services. `AdminMediaController` calls `IMediaService`, while the
public media controller mediates reads through `MediaFile` metadata before
opening `IMediaStorage`. Controllers do not modify EF graphs or manually
inspect roles.

`ProfileController` delegates safe profile reads and name updates to
`ICustomerProfileService`. `OrdersController` continues to delegate owned
creation, list, detail, cancellation, and repeat validation to `IOrderService`.

## Backend folders

```text
Authorization/
Controllers/
Data/
  Configurations/
  Migrations/
DTOs/
  Audit/
  Menu/
    Admin/
    Public/
Entities/
Extensions/
Hubs/
Infrastructure/
  Telegram/
Interfaces/
Middleware/
Services/
  Telegram/
Validators/
```

All entity mapping lives in separate
`IEntityTypeConfiguration<T>` implementations under
`Data/Configurations`. `MoodPickupDbContext` discovers them with
`ApplyConfigurationsFromAssembly`.

## Telegram authentication

`TelegramBotApiClient` is a small typed `HttpClientFactory` client for
`getMe`, `setWebhook`, `getWebhookInfo`, and `sendMessage`; automatic HTTP
logging is removed so the Bot API URL cannot disclose the token.
`TelegramWebhookRegistrationService` validates and registers the bot once at
startup. `TelegramWebhookSecretFilter` authenticates the webhook before
deserialization, while `TelegramUpdateHandler` applies private-chat,
deep-link, contact-ownership, identity-conflict, and durable update-ID rules.
`ITelegramOtpSender` selects either the real delivery implementation or the
Development-only fake without changing the customer authentication contract.

The database remains the authority for challenge state and processed update
IDs. A `Guid RowVersion` protects competing contact updates. Telegram message
delivery is an external side effect: security state and the hashed OTP are
committed before delivery, then `OtpSentAt` is committed after Telegram
accepts the message. A failed delivery clears the hash and locks the challenge.

## Menu domain

The menu aggregate uses:

- `Category` and `Product`;
- storage-independent `MediaFile` metadata;
- reusable `OptionGroup` and `OptionValue` definitions;
- `ProductOptionGroup` and `ProductOptionValue` assignment entities.

Option groups are generic. Size, Milk, and Syrups exist only as development
data, not special code paths.

`MenuConfigurationValidator` handles rules that require a complete loaded
product graph, including cross-group option ownership, duplicate assignment
detection, single-selection defaults, and effective availability. It returns
structured issue codes and does not depend on controllers.

`PublicMenuService` uses `AsNoTracking`, server-side pagination, direct
projections, and batched option queries. Product details deliberately load one
configuration graph. Admin services use `IgnoreQueryFilters()` only for
deleted-resource administration and map EF entities to separate admin DTOs.

Structurally impossible configuration is rejected. A progressively configured
draft may be saved and is returned with an orderability result so multi-step
editing remains possible.

## Persistence behavior

### Configuration

Mappings explicitly define:

- table and key names;
- required fields and maximum lengths;
- `numeric(12,2)` price precision;
- text enum storage;
- foreign keys and restrictive delete behavior;
- check constraints and indexes;
- soft-delete filters;
- optimistic-concurrency tokens.

### Timestamps and normalization

Small interfaces (`IHasTimestamps`, `IHasNormalizedName`, and
`IHasConcurrencyToken`) let the DbContext apply shared persistence behavior
without a base-entity hierarchy. Menu names are trimmed and normalized to
lower-case before persistence. Timestamps use UTC.

### Soft deletion

Category, Product, OptionGroup, OptionValue, and MediaFile use global
soft-delete query filters. Assignment filters mirror required soft-deletable
principals so normal queries cannot accidentally surface orphaned menu
configuration. Historical and administrative code must opt in with
`IgnoreQueryFilters()`.

### Concurrency

Mutable menu records use an application-managed `Guid RowVersion`. EF Core
treats it as a concurrency token and replaces it on each update. This provides
a stable future API value while remaining portable to PostgreSQL and avoiding
SQL Server-specific types.

Customer profiles use the same application-managed GUID concurrency pattern.
Only the name is mutable; a stale update is mapped deliberately to
`PROFILE_VERSION_CONFLICT` rather than the menu conflict fallback.

## Development data

`IDevelopmentMenuSeeder` keeps demo seeding replaceable and testable. Its
implementation:

- exits outside `Development`;
- exits when any menu data already exists;
- inserts one neutral, internally consistent demo graph;
- never overwrites administrator changes;
- does not use migration `HasData`.

Application startup runs migrations only when explicitly configured in
`Development`, then runs the existing administrator seeder and the menu
seeder.

## Error handling

Global exception handling returns RFC 7807 Problem Details and preserves trace
IDs. Menu services raise stable error codes. Explicit stale-token checks and
EF `DbUpdateConcurrencyException` both map to
`409 MENU_VERSION_CONFLICT`; pre-detected conflicts include the current ID and
row version.

## Employee audit

`ICurrentUserContext` reads the authenticated employee ID only from the
validated JWT subject claim. `IEmployeeAuditService` adds a compact
`EmployeeActionLog` to the same DbContext as the business change. A single
`SaveChangesAsync` is atomic for ordinary mutations; reorder, duplication, and
assignment creation use explicit transactions. An audit failure fails the
mutation.

## Employee management and live authorization state

`AdminEmployeesController` and `AdminRolesController` remain thin MVC
controllers behind `CanManageEmployees`. `EmployeeManagementService` owns
search/pagination, username normalization and uniqueness, role resolution,
last-Administrator protection, GUID row-version checks, temporary-password
generation/hashing, soft disable/enable, refresh-session revocation, and safe
audit snapshots. Mutations use explicit serializable transactions and one
business/audit `SaveChangesAsync` commit.

`IsDeleted` is reused as Disabled; no physical-delete path exists. Employee
`RowVersion` follows the existing application-managed GUID concurrency design.
A separate `SessionVersion` changes only for disable/password reset and is
embedded in employee access tokens. Scoped employee authorization handlers
compare current PostgreSQL active state, roles, password-change flag, and
session version with the token. This prevents stale employee tokens from
remaining privileged or becoming valid after re-enable without adding database
work to public/customer requests.

`TemporaryPasswordGenerator` uses `RandomNumberGenerator`, guarantees every
required character class, and shuffles the 18-character result. Only the hash
enters the entity; audit data includes identity/roles/status flags and
revoked-session counts, never a password or token.

## Frontend

The React/Vite frontend uses a nested `/staff` layout with policy-equivalent
capability mapping from employee role claims. TanStack Query owns categories,
products, global options, product configuration, media mutations, and audit
server state. Redux is not used to duplicate those resources.

Focused modules under `src/api/menu`, `src/api/media.ts`, `src/api/audit.ts`,
and `src/api/employees.ts` preserve the shared in-memory
access-token/refresh-cookie client. Pages expose backend validation, accepted
draft orderability issues, and GUID concurrency conflicts rather than
reimplementing business rules. Employee create/reset secrets use direct request
state rather than TanStack Query server caches and disappear on dismissal or
navigation.

The public route uses `src/api/menu/publicMenu.ts` and separate public
TypeScript contract shapes. TanStack Query caches category, filtered product,
and product-detail reads. Search/category parameters are sent to the existing
server projection; the client does not filter product fields or recompute
orderability. It requests pages of 100 and combines additional pages so a
customer can browse the complete grouped catalog. React Router query/hash state
provides shareable category links and browser back/forward restoration.

`PublicProductImage` resolves only API-provided public URLs through the
configured backend origin. Fixed aspect ratios avoid layout shifts, browser
lazy loading handles below-the-fold card media, and load failures replace the
image with an accessible placeholder.

### Frontend cart

TanStack Query remains the owner of categories, product lists, product details,
availability, configuration, and backend orderability. Redux Toolkit owns only
the anonymous local draft: cart lines, selected public option IDs, quantities,
safe snapshots, line states, and storage notices. Public menu responses are not
copied wholesale into Redux.

Pure modules under `src/features/cart` initialize defaults, guide selection,
calculate prices in integer TJS minor units, create canonical identities, parse
and whitelist persisted data, and compare saved lines to current public detail.
Presentation components do not contain these rules.

The `moodpickup.cart.v1` schema persists a currency marker and whitelisted line
snapshots. It omits image/media URLs, tokens, identity, administrative fields,
and all functions/server responses. Meaningful Redux mutations trigger one
storage write; rendering does not. Storage exceptions retain working in-memory
state.

Configuration identity is product ID plus unique, lexically sorted selected
option-value IDs. Add and edit operations merge identical identities and keep
different options separate. Quantity is limited locally to 99 per line and the
cart to 100 distinct configurations as defensive device limits.

Cart revalidation deduplicates product IDs and uses at most four concurrent
uncached detail lookups through `QueryClient.fetchQuery`. This reuses and fills
the ordinary product-detail cache, avoids per-line requests, and does not block
initial menu rendering. Snapshot differences become Updated; invalid
configuration becomes Needs attention; missing/unavailable products remain
visible as Unavailable.

### Checkout and order snapshots

`OrdersController` is customer-policy protected and delegates all creation and
read behavior to `IOrderService`/`OrderService`. The local cart sends only a
product GUID, selected global option-value GUIDs, quantity, and optional
comments. It never sends a trusted price, currency, availability, image, or
customer identity.

The checkout service loads menu records with their soft-delete state, calls the
existing `MenuConfigurationValidator`, validates selection compatibility and
pickup rules, and calculates prices from PostgreSQL. It uses the validated JWT
subject to load the customer profile and stores customer/contact, product
availability, and option fields as immutable order snapshots. There are no
order foreign keys to mutable menu records.

For PostgreSQL, checkout begins a serializable transaction. The daily sequence
uses one atomic `INSERT ... ON CONFLICT ... RETURNING` statement, then the
order, items, and options are saved and committed together. Validation and
concurrency failures leave no partial orders. The public API exposes only
customer-owned records; another customer's ID resolves as `404`.

`CheckoutOptions` is a validated, monitor-backed operational configuration:
currency `TJS`, time zone `Asia/Dushanbe`, hours `10:00-22:00`, and 15-minute
intervals by default. `OrderService` generates today's available slots from the
next future interval through 30 minutes before closing and applies the same
boundary during checkout validation. Replacing operational hours changes slot
generation and validation without a frontend business-rule copy. A staff
configuration UI is intentionally outside this sprint.

### Staff and kitchen workflow with real-time updates

`StaffOrdersController` delegates reception rules to `IStaffOrderService` /
`StaffOrderService`. `StaffKitchenOrdersController` and
`StaffOrderCompletionController` delegate preparation, ETA, payment, and
completion to `IOrderWorkflowService` / `OrderWorkflowService`. This keeps the
established MVC service/controller structure without repositories, MediatR,
or another project layer.

`CanViewKitchen` admits Kitchen, Cashier, Manager, Pickup, and Administrator;
only `CanWorkKitchen` (Kitchen/Administrator) mutates preparation state.
`CanIssueOrders` (Cashier/Pickup/Administrator) records pickup payment and
completion. Customer and MenuManager-only tokens cannot reach these endpoints.

Every mutation carries the current order GUID row version. Pre-detected stale
requests and EF races return `409 ORDER_VERSION_CONFLICT`. Confirmation,
rejection, preparation, ready, ETA, payment, and completion add an
`EmployeeActionLog` to the same DbContext save as the order mutation,
preserving correlation ID, employee, before values, and after values. Every
status transition also inserts append-only `OrderStatusHistory`. The legal
forward path is PendingConfirmation -> Confirmed -> Preparing ->
ReadyForPickup -> Completed; customer cancellation and cafe rejection remain
pending-only. Pay-on-pickup completion requires a separately recorded Cash or
Card receipt; Online orders are initialized paid.

`SignalROrderRealtimeNotifier` publishes confirmation, rejection, preparation,
ETA, ready, payment, and completion events to `customer:{customerId}` and
`staff:all`. Payloads contain an event ID, timestamp, order identity/status,
workflow timestamps, ready time, rejection reason, and payment state, never
employee data. Customer and staff pages invalidate/update TanStack Query,
ignore duplicate event IDs, reconnect automatically, and poll only while the
connection is unavailable.

### Customer profile, history, and repeat ordering

`CustomerProfileService` reads the authenticated subject, projects only safe
profile fields, calculates active/completed counts from owned orders, and
updates a trimmed name with the customer row version. Phone and Telegram
identity remain outside the update contract.

`OrderService.GetMineAsync` composes ownership, lifecycle filter, order-number
or product-snapshot search, newest-first ordering, and pagination in one EF
query. Detail mapping adds all customer-safe workflow/payment timestamps while
continuing to omit employee and audit attribution.

Repeat validation loads the immutable historical items and current menu graph
in the existing service. New snapshots retain nullable historical option
group/value identifiers without foreign keys. Legacy name fallback is accepted
only when exactly one current assignment matches. Each line must retain every
selected option and satisfy current product orderability and group ranges;
otherwise it is returned as unavailable with reasons. Valid results contain
current public identifiers and prices for a local cart line. The HTTP call does
not mutate the server, silently substitute data, or bypass checkout validation.

The frontend keeps profile/order resources in TanStack Query and uses the
existing SignalR hook to invalidate profile counts, filtered histories, and
details. Redux receives repeat lines only after the customer reviews the
validation summary and confirms. The local cart remains anonymous and
device-local.

## File storage

`IMediaStorage` isolates provider operations. `LocalMediaStorage` resolves a
configured root, generates random partitioned keys, verifies every resolved
path remains beneath that root, and performs asynchronous save/open/delete
operations. `IMediaService` owns upload validation, SkiaSharp decode/re-encode,
metadata/audit persistence, and cleanup on failure.

SkiaSharp is required because signature checks alone cannot prove that the
whole image decodes safely or strip metadata/trailing content. Uploads are
fully decoded as one JPEG, PNG, or WebP frame, bounded by encoded bytes,
dimensions, and estimated decoded bytes, then re-encoded without resizing.
The cross-platform native assets package supports the Linux container. The
dependency is MIT licensed.

`GET /media/{storageKey}` does not use directory browsing or unrestricted
static-file middleware. It validates a non-deleted metadata row and local
object, returns the verified content type, range support, an ETag, and long
immutable caching. Physical paths are never returned.

## Testing

- Existing WebApplicationFactory authentication tests continue using their
  isolated test provider.
- Menu domain tests retain SQLite for fast provider-neutral checks.
- Critical HTTP integration tests use
  `MOODPICKUP_POSTGRES_TEST_CONNECTION` and migrate a real PostgreSQL database.
  They cover projections, policy enforcement, filtered uniqueness,
  transactions/audit rollback, soft deletion, assignment drafts, and stale
  GUID row versions. Sprint 3.3 adds isolated temporary-media coverage for
  authorization, JPEG/PNG/WebP, unsafe inputs, metadata consistency,
  audit rollback, retrieval, traversal defense, and public product URLs.
- Vitest/React Testing Library covers staff authorization and administrative
  behavior at the HTTP boundary. Sprint 3.4 adds public catalog coverage.
  Sprint 3.5 adds configuration, decimal-safe price, Redux operations,
  persistence recovery/failure, canonical merge/edit, cart UI, cache reuse,
  bounded revalidation, stale lines, and semantic control coverage.
- Sprint 3.6 adds order snapshot/validation unit coverage, PostgreSQL checkout
  ownership and rollback coverage, and React checkout/navigation/cart-clearing
  coverage.
- Sprint 3.7 adds staff transition/audit/notification/concurrency unit tests,
  PostgreSQL authorization and workflow tests, plus React staff-dialog,
  navigation, customer-status, and SignalR cache-update coverage.
- Sprint 3.8 adds forward workflow/history/payment/ETA/concurrency unit tests,
  real-PostgreSQL policy and endpoint tests, and React kitchen, completion,
  navigation, responsive-state, and expanded SignalR coverage.
- Sprint 3.9 adds profile validation/concurrency/count tests, PostgreSQL
  ownership/filter/search/repeat tests, and React profile, navigation, history,
  detail, timeline, and reviewed repeat-cart coverage.
- Sprint 4.0 adds real-PostgreSQL Administrator authorization, generated
  password/first-login, multi-role update, last-Administrator, concurrency,
  disable/enable, session/access-token invalidation, reset, secret-free audit,
  and action-history coverage. React tests cover protected navigation,
  responsive list/filter/pagination, multi-role create, one-time copy, edit,
  conflicts, reset/session feedback, and last-Administrator messaging.
- Docker smoke validation covers the complete composed stack.

No EF Core InMemory tests are used to claim relational menu behavior.

## Design constraints

The backend intentionally does not use:

- Clean Architecture projects;
- MediatR or CQRS;
- repositories or Unit of Work wrappers;
- AutoMapper;
- event sourcing;
- speculative entity hierarchies.

Business logic belongs in services, controllers remain thin, DTOs cross API
boundaries, and asynchronous APIs accept cancellation tokens where relevant.

## Sprint 4.1 payment boundary

`IPaymentProvider` isolates provider launch, status verification, and the
future full-refund operation. `AlifPaymentProvider` implements only official
WebCheckout form launch and `/checktxn`; its refund method fails explicitly
without network activity until Alif confirms the cancellation contract.
`PaymentService` owns persistence, state transitions, idempotency, audit, and
SignalR. Controllers remain thin and the existing one-project MVC structure is
unchanged.

## Future evolution

Later work can add persisted notification history, more payment providers, official refunds,
discounts, delivery, cloud media storage,
responsive image derivatives, and full-text search without replacing the
current order API contract or local cart selection model.
