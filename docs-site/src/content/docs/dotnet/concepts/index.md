---
title: Concepts
description: Core concepts and design principles behind Granit — module system, persistence, messaging, security, and compliance
sidebar:
  order: 0
---

Granit is not a collection of independent utilities. Every design decision flows from
a small set of principles: modules compose with explicit dependency declarations, data
is always scoped to its tenant, events cross boundaries without coupling, and compliance
is structural rather than bolted on.

Understanding these eleven concepts gives you the mental model to use Granit effectively
and extend it without surprises.

## How concepts connect

```mermaid
graph TD
    MS[Module System] --> DI[Dependency Injection]
    MS --> Bundles
    DI --> Config[Configuration]
    DI --> P[Persistence]
    P --> MT[Multi-Tenancy]
    P --> C[Compliance]
    MS --> Msg[Messaging]
    Msg --> WO[Wolverine Optionality]
    Msg --> C
    MT --> SM[Security Model]
    SM --> C
```

Start with the **Module System** — every other concept builds on it.

## Core architecture

- [Module System](./module-system/) — `[DependsOn]`, topological sort, conditional modules,
  two-phase lifecycle
- [Dependency Injection](./dependency-injection/) — module service registration, Options
  pattern, PostConfigure
- [Configuration](./configuration/) — Options (startup), Settings (runtime), Module Config
  (frontend read-only)
- [Bundles](./bundles/) — meta-packages and the fluent `GranitBuilder` API for quick onboarding

## Data and infrastructure

- [Persistence](./persistence/) — isolated DbContext, interceptors (audit, soft delete,
  versioning), automatic query filters
- [Multi-Tenancy](./multi-tenancy/) — three isolation strategies, transparent query filters,
  async-safe tenant context
- [Messaging](./messaging/) — domain events, integration events, transactional outbox,
  automatic context propagation
- [Wolverine Optionality](./wolverine-optionality/) — Channel fallback, crash behavior,
  when you actually need a message bus

## Security and compliance

- [Security Model](./security-model/) — JWT authentication, provider-agnostic RBAC,
  per-role caching, back-channel logout
- [Compliance](./compliance/) — GDPR and ISO 27001 enforcement as structural patterns

## Architecture decisions

- [Modular Monolith vs Microservices](./modular-monolith-vs-microservices/) — decision
  framework, migration path, operational trade-offs
