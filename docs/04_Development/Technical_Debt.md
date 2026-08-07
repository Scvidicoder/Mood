# Technical Debt

Version: 1.0

## React Router RSC advisory

`npm audit` currently reports two high-severity findings through
`react-router-dom` and `react-router`. Both point to
`GHSA-qwww-vcr4-c8h2`, “RSC Mode CSRF Bypass Allows Action Execution Before
400 Response,” affecting React Router versions from 7.12.0 before 8.3.0.

Mood Pickup is currently a client-only React/Vite single-page application. It
does not enable React Server Components, framework actions, or React Router RSC
mode, so the vulnerable execution path is not reachable in the current
architecture. Downgrading with `npm audit fix --force` would introduce a
breaking dependency change and is intentionally prohibited.

Revisit this item when:

- React Router publishes a compatible fixed release;
- the frontend adopts server rendering, React Server Components, or actions;
- the routing architecture changes; or
- before a production security review.

Run `npm audit` during each dependency review. Upgrade normally once a
compatible fixed release is available.

## HS256 signing

Sprint 2 uses HS256 with a sufficiently long key supplied through configuration.
This matches the approved initial deployment decision and keeps real keys out
of source control. Replace the signing implementation with RS256 before public
key distribution, independently deployed token consumers, or broader
production scaling requires asymmetric verification. The authentication API
contract must remain unchanged.

## Common-password denylist

Password validation currently uses a small, deterministic,
configuration-driven denylist. This is testable and has no external service
dependency, but it cannot provide the coverage of a large breached-password
corpus. Revisit before production employee onboarding. A future implementation
may use an offline breached-password dataset or privacy-preserving lookup
service after its operational and privacy requirements are documented.

## Telegram relinking and delivery guarantees

Real Telegram contact linking and OTP delivery are implemented. Development
still permits the explicit fake sender, which logs OTPs only in `Development`
and is rejected elsewhere.

There is no self-service relinking flow yet. A customer whose phone is already
linked to a different Telegram identity, or a Telegram identity already owned
by another customer, is locked out of that challenge without revealing the
other account. A future authenticated, audited recovery/relinking workflow
must define additional proof, session revocation, and support procedures.

Telegram delivery is an external side effect and cannot participate in the
PostgreSQL transaction. The implementation commits verified linking state and
the OTP hash before `sendMessage`, then records `OtpSentAt` after Telegram
accepts the message; a reported send failure clears the hash and locks the
challenge. Durable `update_id` markers and challenge concurrency prevent
ordinary duplicate delivery, but an infrastructure failure in the narrow
window after Telegram accepts the message and before `OtpSentAt` commits
cannot provide exactly-once semantics. A future transactional outbox would
need encrypted short-lived OTP material and explicit key/retention controls;
that added secret-storage design is intentionally not introduced here.

## Menu audit retention and export

Sprint 3.2 writes append-only `EmployeeActionLog` rows transactionally with
every menu mutation and exposes administrator list/detail reads. A retention
period, archive/export workflow, and tamper-evident storage are not yet
specified. Define those operational requirements before audit volume becomes
material. Audit mutation endpoints must remain unavailable.

## Cross-table menu constraints

PostgreSQL constraints enforce local ranges, foreign keys, and assignment
uniqueness. Rules that require another table or a count—such as confirming that
a product assignment points to a value from the same global group or that a
single-selection group has at most one default—are enforced by
`MenuConfigurationValidator`.

Every future menu mutation path must call this validator before saving. If menu
data will be changed by tools outside the API, evaluate transaction-safe
database triggers or a restricted database role rather than duplicating
business rules ad hoc.

## Public menu query performance

Sprint 3.2 uses no-tracking projections, server-side pagination, and batched
option queries; it intentionally introduces no cache. Measure production query
latency and catalog size before considering output caching or Redis. Any future
cache must invalidate after successful audited mutations and must not change
visibility/orderability semantics.

Sprint 3.4 loads the customer catalog in API pages of 100 and combines them so
the homepage can group the entire configured menu. This is appropriate for the
current small café catalog. If measured production menus become materially
larger, move to section-level/infinite loading or a purpose-built grouped
projection while preserving search, ordering, and visibility semantics.

## Menu search

Sprint 3.1 uses deterministic lower-case `NormalizedName` columns and ordinary
B-tree indexes. This supports reliable normalized lookup and the required
active-value uniqueness but is not full-text or typo-tolerant search. Before
large catalogs or advanced search are introduced, evaluate PostgreSQL
full-text search or `pg_trgm` based on measured query requirements.

## Media storage and processing

Sprint 3.3 implements validated local storage behind `IMediaStorage`, safe
random keys, decode/re-encode metadata stripping, and metadata-mediated public
delivery. `MediaFile` still stores provider-independent metadata only.

The following remain deliberately open:

- reference-aware orphan detection and administrative cleanup;
- responsive image resizing and generated thumbnails;
- a cloud object-storage provider behind `IMediaStorage`;
- antivirus or malware scanning as an additional production-hardening layer;
- richer decompression-bomb controls and resource isolation beyond configured
  encoded-size, dimension, frame-count, and decoded-byte limits.

Replacing or unassigning a product image does not delete the old object because
another product may reference it and recovery/audit may require it.

## Frontend API client generation

Sprint 3.3 uses carefully maintained TypeScript contracts and focused API
modules. Evaluate OpenAPI client generation when contract breadth makes manual
maintenance costly. A generator must preserve the current refresh-cookie,
in-memory access-token, cancellation, and `ProblemDetails` behavior.

## Frontend end-to-end testing

Vitest and React Testing Library cover staff authorization, administrative
workflows, Sprint 3.4 public-menu behavior, and Sprint 3.5 configuration/cart
behavior at the HTTP boundary and pure-model layers. Sprint 3.4 and Sprint 3.5
also include documented manual real-browser passes across the Docker stack.
An automated browser-driven suite for responsive layout, network throttling,
storage failure, and keyboard-only accessibility remains future work.

## Local cart authority and synchronization

Sprint 3.5's cart is deliberately anonymous, device-local, and stored only
under `moodpickup.cart.v1`. It is not synchronized across browsers/devices,
associated with a customer account, stored on the backend, or a stock
reservation. Clearing site data removes it. Storage quota/private-mode
failures keep only the current in-memory copy.

Product names, option labels, prices, modifiers, and availability inside the
cart are untrusted display snapshots. Public detail revalidation provides
customer guidance but cannot lock commercial truth. Sprint 3.6 must submit only
public product/option identifiers and quantity, then recalculate price and
revalidate product visibility, availability, orderability, option constraints,
pickup rules, and stock on the backend. Clear the local cart only after
successful order creation.

The current public contract exposes optional `volumeMilliliters` and `calories`
on selected product option values, but does not distinguish additive modifiers
from absolute configured values. The Sprint 3.5 UI treats a single unambiguous
selected value as an explicit configured value and otherwise keeps the base
metric while continuing to show per-option metadata. Before options need
combined nutritional arithmetic, document additive/override semantics and
extend the contract backward-compatibly rather than guessing in the client.

The local client safety limits are 99 units per configuration and 100 distinct
configurations. These defend browser state size and accidental input; they are
not backend business guarantees. Sprint 3.6 must define authoritative order
limits.

## Public brand assets and responsive media

The repository does not yet contain an approved Mood logo, color token package,
or product photography set. Sprint 3.4 therefore uses a text/CSS brand mark and
the secure placeholder path rather than inventing or embedding unapproved
assets. Add approved source assets and usage rules when they are provided.

Public product cards currently download the original normalized upload. Add
server-generated responsive derivatives and `srcset` after image dimensions,
quality, retention, and cache invalidation rules are approved.

## Application-managed menu concurrency

Menu updates use a random `Guid RowVersion` rather than PostgreSQL `xmin`.
This creates stable API-friendly tokens and makes relational tests portable.
If future write volume makes UUID index/update cost material, benchmark `xmin`
or another PostgreSQL-native strategy while preserving the API `rowVersion`
contract.

## Development menu seed

The seeded Coffee/Tea/Cold Drinks/Breakfast/Desserts data is neutral demo data,
not the official Mood menu. Seeding intentionally stops when any menu data
exists, which protects administrator changes but does not repair a partially
deleted or manually incomplete demo graph. Developers who need a fresh demo
must use a fresh development database.
