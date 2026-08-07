
# Developer Guide

Version: 1.0

## Purpose

This document defines mandatory engineering rules for anyone implementing Mood Pickup System, including AI coding assistants.

---

# 1. Source of Truth

Implementation must follow the documentation in `/docs`.

If documentation conflicts with generated code, documentation wins.

Do not invent business rules.

---

# 2. Architecture Rules

- Backend: ASP.NET Core Web API
- Frontend: React + TypeScript
- PostgreSQL
- SignalR
- Docker

Business logic belongs only in services.

Controllers must:
- validate
- authorize
- call services
- return responses

---

# 3. Coding Rules

- Async all the way
- CancellationToken for async APIs
- Nullable enabled
- No duplicated logic
- DTOs across API boundaries
- FluentValidation for validation
- Global exception handling
- Structured logging

---

# 4. Database Rules

- EF Core migrations only
- No manual schema edits
- Immutable order snapshots
- Soft delete where specified
- Optimistic concurrency with RowVersion

---

# 5. Frontend Rules

- Mobile first
- Feature-based folders
- React Query for server state
- Redux Toolkit only for global client state
- Reusable UI components
- No business logic inside presentation components
- Public menu search/filtering must use the server projection
- Public orderability and availability messages must come from the API
- URL query/hash state is the source of truth for shareable menu filters
- Product images must use API media URLs and retain accessible fallbacks
- TanStack Query owns current public menu/detail server state
- Redux Toolkit owns only global cart client state; do not copy API resources
  wholesale into Redux
- The anonymous cart may persist only its versioned non-sensitive whitelist
  under `moodpickup.cart.v1`
- Cart price/availability snapshots are never backend or checkout truth
- Access, refresh, and CSRF tokens remain forbidden in browser storage

---

# 6. API Rules

Every endpoint must:

- validate input
- authorize access
- return ProblemDetails-compatible errors
- be documented
- support cancellation where applicable

---

# 7. Security

- Never log passwords, OTPs or tokens
- Validate ownership of customer resources
- Enforce role policies
- Secrets only from environment variables

---

# 8. Testing Checklist

Before considering a feature complete:

- Business rules verified
- API tested
- UI tested
- SignalR verified (if applicable)
- Audit logging verified
- Authorization verified

---

# 9. Git

Recommended branch strategy:

- main
- develop
- feature/*
- bugfix/*

Small focused commits.

---

# 10. AI Development Rules

AI assistants must:

- Read relevant documentation before coding
- Avoid changing architecture without explicit approval
- Keep changes scoped to the requested feature
- Update documentation when behavior changes
- Prefer maintainable code over clever code
- Do not introduce undocumented dependencies

---

# Definition of Done

A task is complete only when:

- Code builds successfully
- Documentation remains accurate
- No compiler warnings introduced
- Manual happy-path tested
- Business rules satisfied
