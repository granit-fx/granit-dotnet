---
title: "Architecture, Design Patterns & ADRs"
description: Architectural decisions, design patterns, and ADRs for Granit — modular .NET 10 framework with hexagonal architecture, CQRS, and multi-tenancy support.
sidebar:
  label: Architecture
  order: 0
---

This section documents the architectural decisions and design patterns used
throughout the Granit framework.

## Sections

- **[Pattern Library](./patterns/)** — 51 design patterns with their concrete
  implementation in Granit, organized by category (architecture, cloud/SaaS,
  GoF, data, concurrency, .NET idioms, security)
- **[ADRs](./adr/)** — 16 Architecture Decision Records documenting key
  technology choices (Serilog, Redis, Wolverine, Scriban, ClosedXML, etc.)

## Design principles

Granit is built on a set of explicit architectural principles:

- **Convention over configuration** -- sensible defaults, explicit overrides
- **Module isolation** -- each module owns its DbContext, its DI registrations,
  and its public API surface
- **CQRS everywhere** -- `IReader` and `IWriter` interfaces are never merged
- **Soft dependencies** -- modules access cross-cutting concerns (tenancy, time,
  user context) via `Granit.Core` interfaces, not direct package references
- **Compliance by design** -- GDPR and ISO 27001 constraints are
  architectural decisions, not afterthoughts
