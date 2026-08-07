# Database Design

Version: 1.4 (Real Telegram authentication)

## Overview

Mood Pickup uses PostgreSQL through EF Core migrations. The current database
contains the authentication schema, the Sprint 3.1 menu domain, and
the Sprint 3.2 employee menu audit log. Public/admin menu HTTP APIs are
implemented. `RealTelegramAuthentication` extends login challenges and adds
durable Telegram update idempotency. Sprint 3.3 adds filesystem media behavior without changing the
database schema: PostgreSQL continues to store metadata only. Sprint 3.5 adds
an anonymous browser-local cart and does not change PostgreSQL. Backend carts,
orders, payments, and notification storage are not implemented.

The database is designed for one cafe. Future orders will store immutable menu
snapshots so historical prices and names do not depend on current menu records.

## Core principles

1. EF Core migrations are the only schema-change mechanism.
2. Menu business records use soft deletion where history matters.
3. Shared option values contain no product-specific price or availability.
4. Money uses PostgreSQL `numeric(12,2)`.
5. Timestamps use UTC `timestamp with time zone`.
6. Physical cascade deletion is minimized for menu data.
7. Structured service validation covers rules that span multiple tables.

## Authentication entities

### `Customers`

- `Id` (`uuid`)
- `Name` (required, maximum 100)
- `PhoneNumber` (required, unique, maximum 16)
- `TelegramChatId` (nullable, unique when present)
- `CreatedAt`
- `UpdatedAt`

### `Employees`

- `Id`
- `Username` (required, unique, maximum 64)
- `PasswordHash` (required, maximum 512)
- `FullName` (required, maximum 100)
- `IsAdmin`
- `MustChangePassword`
- `IsDeleted`
- `CreatedAt`
- `UpdatedAt`

### `Roles` and `EmployeeRoles`

`Roles.Name` is unique. `EmployeeRoles` has a composite primary key of
`EmployeeId + RoleId`.

### `RefreshTokens`

- `Id`
- `FamilyId`
- `AccountType`
- exactly one of `CustomerId` or `EmployeeId`
- `TokenHash` (unique)
- `CreatedAt`
- `ExpiresAt`
- `RevokedAt`
- `ReplacedByTokenId`
- `CreatedByIpHash`
- `RevokedByIpHash`
- `UserAgentHash`

Only token hashes are stored. The account-owner check constraint requires
exactly one owner.

### `LoginChallenges`

- `Id`
- `PhoneNumber`
- `CodeHash`
- `TelegramChatId`
- `TelegramUserId`
- `TelegramUsername`
- `TelegramLinkTokenHash`
- `TelegramLinkExpiresAt`
- `TelegramStartedAt`
- `TelegramLinkUsedAt`
- `TelegramLinkedAt`
- `TelegramContactVerifiedAt`
- `TelegramLinkAttemptCount`
- `TelegramDeliveryFailureCount`
- `TelegramDeliveryFailedAt`
- `ClientStatusSecretHash`
- `OtpSentAt`
- `CreatedAt`
- `ExpiresAt`
- `AttemptCount`
- `MaximumAttempts`
- `IsUsed`
- `LastSentAt`
- `Purpose`
- `RequestIpHash`
- `UserAgentHash`
- `RowVersion`

`CodeHash` is null until Telegram ownership is verified in real mode. Raw OTP,
deep-link token, and client status secret values are never stored; only
domain-separated hashes are persisted. Link-token and status-secret hashes
have filtered unique indexes. `RowVersion` protects competing webhook updates.

### `TelegramProcessedUpdates`

- `UpdateId` (`bigint`, primary key)
- `ProcessedAt`

The primary key makes Telegram webhook processing idempotent across instances.
A background service removes markers older than the configured retention
window; entire Telegram update payloads are not retained.

## Menu entities

### `Categories`

- `Id` (`uuid`)
- `Name` (required, maximum 120)
- `NormalizedName` (required, maximum 120)
- `Description` (nullable, maximum 500)
- `DisplayOrder` (non-negative)
- `IsVisible`
- `IsDeleted`
- `CreatedAt`
- `UpdatedAt`
- `RowVersion` (`uuid`)

Category names are intentionally not unique. This avoids an undocumented
global naming restriction while still providing a normalized-name lookup
index.

### `Products`

- `Id`
- `CategoryId` (required)
- `Name` (required, maximum 160)
- `NormalizedName` (required, maximum 160)
- `ShortDescription` (nullable, maximum 300)
- `Description` (nullable, maximum 2,000)
- `Ingredients` (nullable, maximum 1,000)
- `BasePrice` (`numeric(12,2)`, non-negative)
- `DefaultWeightGrams` (nullable, non-negative)
- `DefaultVolumeMilliliters` (nullable, non-negative)
- `DefaultCalories` (nullable, non-negative)
- `ImageId` (nullable)
- `IsAvailable`
- `IsVisible`
- `IsDeleted`
- `DisplayOrder` (non-negative)
- `CreatedAt`
- `UpdatedAt`
- `RowVersion`

Every product belongs to exactly one category. A product may have no option
groups. `ImageId` provides at most one primary image per product; no public URL
is stored.

### `MediaFiles`

- `Id`
- `StorageProvider` (required, maximum 32)
- `StorageKey` (required, maximum 512)
- `OriginalFileName` (required, maximum 255)
- `ContentType` (required, maximum 100)
- `FileSizeBytes` (non-negative)
- `Width` (nullable, positive when present)
- `Height` (nullable, positive when present)
- `CreatedAt`
- `CreatedByEmployeeId` (nullable)
- `IsDeleted`

The table stores metadata only. Raw image bytes and permanent public URLs are
not stored. `StorageProvider + StorageKey` is unique, allowing local or cloud
storage providers without a schema redesign. Sprint 3.3 validates and
normalizes an image before writing a local object, then inserts this record and
an employee audit row in one database unit. A failed metadata/audit save removes
the new object. Product replacement/unassignment does not delete the old
record or object because media may be shared.

No Sprint 3.3 migration is required: every field needed for storage provider,
safe key, verified type/size/dimensions, creator, timestamp, and deletion state
already exists in the Sprint 3.1 schema.

### `OptionGroups`

- `Id`
- `Name` (required, maximum 120)
- `NormalizedName` (required, maximum 120)
- `Description` (nullable, maximum 500)
- `SelectionType` (`Single` or `Multiple`, stored as text)
- `DefaultIsRequired`
- `DefaultMinimumSelections` (non-negative)
- `DefaultMaximumSelections` (nullable, positive when present)
- `DisplayOrder` (non-negative)
- `IsActive`
- `IsDeleted`
- `CreatedAt`
- `UpdatedAt`
- `RowVersion`

The default minimum cannot exceed the maximum. A required group has a minimum
of at least one. A `Single` group cannot have a default maximum greater than
one.

### `OptionValues`

- `Id`
- `OptionGroupId` (required)
- `Name` (required, maximum 120)
- `NormalizedName` (required, maximum 120)
- `Description` (nullable, maximum 500)
- `DisplayOrder` (non-negative)
- `IsActive`
- `IsDeleted`
- `CreatedAt`
- `UpdatedAt`
- `RowVersion`

Values are global and reusable. Product price and availability are deliberately
absent. Active values are unique by
`OptionGroupId + NormalizedName` using a filtered unique index where
`IsDeleted = false`.

### `ProductOptionGroups`

- `Id`
- `ProductId` (required)
- `OptionGroupId` (required)
- `IsRequired`
- `MinimumSelections` (non-negative)
- `MaximumSelections` (positive)
- `DisplayOrder` (non-negative)
- `IsActive`
- `CreatedAt`
- `UpdatedAt`
- `RowVersion`

`ProductId + OptionGroupId` is unique. Product selection rules override the
global defaults. Cross-table rules such as a `Single` assignment requiring a
maximum of exactly one are enforced by `MenuConfigurationValidator`.

### `ProductOptionValues`

- `Id`
- `ProductOptionGroupId` (required)
- `OptionValueId` (required)
- `PriceModifier` (`numeric(12,2)`, non-negative)
- `IsDefault`
- `IsAvailable`
- `DisplayOrder` (non-negative)
- `VolumeMilliliters` (nullable, non-negative)
- `Calories` (nullable, non-negative)
- `CreatedAt`
- `UpdatedAt`
- `RowVersion`

`ProductOptionGroupId + OptionValueId` is unique. Default-count,
group-ownership, and effective-availability rules require related rows and are
therefore enforced by `MenuConfigurationValidator`.

### `EmployeeActionLogs`

- `Id`
- `EmployeeId` (required)
- `ActionType` (maximum 80)
- `EntityType` (maximum 80)
- `EntityId`
- `Description` (maximum 500)
- `OldValuesJson` (`jsonb`, nullable)
- `NewValuesJson` (`jsonb`, nullable)
- `CreatedAt`
- `CorrelationId` (maximum 100)

Menu audit records are append-only and written in the same unit of database
work as each mutation. JSON contains only relevant changed menu fields, never
authentication secrets or large entity graphs. There is no audit update/delete
API. The employee foreign key is restrictive so soft-deleting an employee
preserves history.

## Relationships and delete behavior

| Relationship | Cardinality | Physical delete behavior |
| --- | --- | --- |
| Category to Product | one-to-many | `Restrict` |
| MediaFile to Product image | one-to-many | `SetNull` |
| Employee to created MediaFile | one-to-many | `SetNull` |
| OptionGroup to OptionValue | one-to-many | `Restrict` |
| Product to ProductOptionGroup | one-to-many | `Restrict` |
| OptionGroup to ProductOptionGroup | one-to-many | `Restrict` |
| ProductOptionGroup to ProductOptionValue | one-to-many | `Restrict` |
| OptionValue to ProductOptionValue | one-to-many | `Restrict` |
| Employee to EmployeeActionLog | one-to-many | `Restrict` |

Menu records are not physically cascade-deleted. Existing Sprint 2 cascades
for authentication-owned refresh tokens and employee-role joins are unchanged.

## Indexes and constraints

### Menu listing and search indexes

- `Categories(IsDeleted, IsVisible, DisplayOrder)`
- `Categories(NormalizedName)`
- `Products(CategoryId, IsDeleted, IsVisible, DisplayOrder)`
- `Products(IsDeleted, IsAvailable)`
- `Products(NormalizedName)`
- `OptionGroups(IsDeleted, IsActive, DisplayOrder)`
- `OptionGroups(NormalizedName)`
- `OptionValues(OptionGroupId, IsDeleted, IsActive, DisplayOrder)`
- `ProductOptionGroups(ProductId, IsActive, DisplayOrder)`
- `ProductOptionValues(ProductOptionGroupId, IsAvailable, DisplayOrder)`

Foreign-key indexes are also present for media creators, product images,
option-group assignments, and option-value assignments.

### Audit indexes

- `EmployeeActionLogs(CreatedAt, Id)`
- `EmployeeActionLogs(EmployeeId, CreatedAt)`
- `EmployeeActionLogs(ActionType, CreatedAt)`
- `EmployeeActionLogs(EntityType, EntityId, CreatedAt)`

### Unique indexes

- `MediaFiles(StorageProvider, StorageKey)`
- active `OptionValues(OptionGroupId, NormalizedName)` where not deleted
- `ProductOptionGroups(ProductId, OptionGroupId)`
- `ProductOptionValues(ProductOptionGroupId, OptionValueId)`

### Check constraints

Database checks reject blank/untrimmed names, negative display orders, negative
prices or dimensions, invalid group ranges, non-positive maxima, and required
groups with a zero minimum. Rules that require joining to the global group or
counting defaults remain service-level validations.

## Name normalization

The DbContext trims menu names and writes `NormalizedName` as
`Name.ToLowerInvariant()` before insert or update. This avoids a `citext`
dependency and makes case-insensitive equality/uniqueness deterministic.
Category and product names are indexed but not unique. Sprint 3.1 does not
introduce PostgreSQL full-text search.

## Soft deletion and query filters

Global query filters exclude deleted:

- categories;
- products;
- media files;
- option groups;
- option values.

Dependent `ProductOptionGroup` and `ProductOptionValue` queries also exclude
assignments whose required soft-deletable principal is deleted. Soft deletion
never physically removes products or assignments.

Administrative and historical code must call `IgnoreQueryFilters()` explicitly
and deliberately when retrieving deleted rows. Authentication entities are not
included in menu filters.

## Timestamps and concurrency

`MoodPickupDbContext` applies UTC timestamps through `IHasTimestamps`.
`CreatedAt` is assigned only on insert and is protected from modification;
`UpdatedAt` changes on every mutable menu update.

Mutable menu entities use an application-managed `Guid RowVersion` configured
as an EF Core concurrency token. Each successful insert receives a token and
each update replaces it. EF includes the original token in update predicates,
so a stale writer receives `DbUpdateConcurrencyException`. This is
PostgreSQL-safe and can later map to the documented API `rowVersion` string
without using SQL Server `rowversion` or exposing PostgreSQL `xmin`.

Concurrency is enabled for `Category`, `Product`, `OptionGroup`, `OptionValue`,
`ProductOptionGroup`, and `ProductOptionValue`.

## Development seed

`DevelopmentMenuSeeder` runs only when the environment is `Development` and
only when all menu tables are empty. This protects administrator changes and
makes restarts idempotent. The neutral demo seed is not an official Mood menu.

It creates:

- categories: Coffee, Tea, Cold Drinks, Breakfast, Desserts;
- products: Cappuccino, Latte, Americano, Cheesecake, Croissant;
- shared groups: Size, Milk, Syrups;
- nine shared option values;
- a configurable Cappuccino with required Size and Milk defaults and optional
  Syrups;
- an unavailable Latte;
- an unavailable Coconut Milk product assignment;
- simple products with no option groups.

Seed data is created by a startup service, not migration `HasData`, and is not
run in Testing or Production.

## Current migrations

- `20260805090000_InitialFoundation`
- `20260806062531_Sprint2AuthenticationSecurity`
- `20260806072221_Sprint3MenuDomain`
- `20260806081107_Sprint3MenuApiAudit`
