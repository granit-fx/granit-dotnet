---
title: Dependency Graph
description: Package dependency visualization and module relationship map for all 97 Granit packages
sidebar:
  order: 32
---

This page documents the dependency graph across all %%PACKAGE_COUNT%% Granit source packages. Arrows
indicate the direction of usage: `A --> B` means "A is used by B". `Granit.Core`
is the root and feeds the entire tree.

Conventions used throughout this page:

- **Diagram arrows** flow from foundation to consumers (`Core → Security → Wolverine`).
  Tables still list dependencies from the consumer's perspective ("Depends on").
- Transitive dependencies on `Granit.Core` are omitted when a package already depends
  on another module that depends on Core.
- The systematic pattern `*.Endpoints → Authorization` is omitted from the overview
  diagram (documented in the coupling rules section).
- `*.EntityFrameworkCore` packages are leaf nodes unless stated otherwise.

## High-level overview

Each node represents a functional domain with the package count in parentheses.

```mermaid
flowchart TD
    CORE["Core (1)"]

    subgraph Foundation
        UTILS["Utilities (13)"]
        SEC["Security (12)"]
        CACHE["Caching (3)"]
        IDENT["Identity (5)"]
    end

    subgraph Infrastructure
        PERS["Persistence (3)"]
        WOL["Wolverine (3)"]
    end

    subgraph Functional
        LOC["Localization (4)"]
        WEB["Web, API, Webhooks (9)"]
        CONFIG["Configuration (8)"]
        STORAGE["Storage (9)"]
    end

    subgraph Business
        TMPL["Templating (8)"]
        QRY["Querying (3)"]
        DX["DataExchange (6)"]
        WF["Workflow (4)"]
        NOTIF["Notifications (15)"]
        TL["Timeline (4)"]
        JOBS["Background Jobs (4)"]
    end

    ANLZ["Analyzers (2)"]

    CORE --> UTILS
    CORE --> SEC
    CORE --> CACHE
    CORE --> LOC

    CACHE --> SEC
    SEC --> PERS
    UTILS --> PERS
    UTILS --> STORAGE

    SEC --> WOL
    PERS --> WOL
    PERS --> LOC
    PERS --> QRY
    PERS --> WF
    CORE --> IDENT
    PERS --> IDENT

    SEC --> WEB
    CACHE --> WEB
    CACHE --> CONFIG
    LOC --> CONFIG
    PERS --> CONFIG

    UTILS --> TMPL
    QRY --> NOTIF
    WOL --> DX
    WOL --> JOBS
    SEC --> TL
    SEC --> JOBS
    QRY --> DX
    WF --> TMPL
    NOTIF --> WF
    NOTIF --> TL
    IDENT --> WF
```

### Domain composition

| Domain | Packages |
|--------|----------|
| Utilities | Timing, Guids, Diagnostics, Validation, Validation.Europe, ExceptionHandling, Observability, MultiTenancy, Privacy, Cors, Bulkhead, RateLimiting, Querying |
| Identity | Identity, Identity.Keycloak, Identity.EntraId, Identity.Cognito, Identity.EntityFrameworkCore, Identity.Endpoints |
| Security | Security, Encryption, Vault, Vault.HashiCorp, Vault.Azure, Vault.Aws, Auth.JwtBearer, Auth.Keycloak, Auth.EntraId, Auth.Cognito, Auth.ApiKeys (3), Authorization, Authorization.EF, Authorization.Endpoints |
| Configuration | Settings (3), Features (2), ReferenceData (3) |
| Web, API, and Webhooks | ApiVersioning, ApiDocumentation, Cookies, Cookies.Klaro, Cookies.Endpoints, Idempotency, Webhooks (3) |
| Storage | BlobStorage (7), Imaging (2) |
| Background Jobs | BackgroundJobs (4) |
| Localization | Localization, Localization.EntityFrameworkCore, Localization.Endpoints, Localization.SourceGenerator |
| Templating | Templating, Templating.Scriban, Templating.EF, Templating.Endpoints, Templating.Workflow, DocumentGeneration, DocumentGeneration.Pdf, DocumentGeneration.Excel |
| Notifications | Notifications, Notifications.EF, Notifications.Endpoints, Notifications.Wolverine, Email, Email.Smtp, Email.AzureCommunicationServices, Sms, Sms.AzureCommunicationServices, WhatsApp, WebPush, SignalR, Sse, Zulip, Brevo, MobilePush, MobilePush.GoogleFcm, MobilePush.AzureNotificationHubs |
| Workflow | Workflow, Workflow.EF, Workflow.Endpoints, Workflow.Notifications |
| Timeline | Timeline, Timeline.EF, Timeline.Endpoints, Timeline.Notifications |
| DataExchange | DataExchange, DataExchange.Csv, DataExchange.Excel, DataExchange.EF, DataExchange.Endpoints, DataExchange.Wolverine |

## Core layer dependencies

The backbone of the framework: security, distributed cache, and data persistence.

```mermaid
flowchart TD
    CORE["Core"]

    subgraph Primitives
        TIMING["Timing"]
        GUIDS["Guids"]
        EXC["ExceptionHandling"]
        ENCR["Encryption"]
    end

    subgraph Security
        SEC["Security"]
        JWT["Auth.JwtBearer"]
        KC["Auth.Keycloak"]
        ENTRA["Auth.EntraId"]
        APIKEYS["Auth.ApiKeys"]
        APIKEYS_EP["Auth.ApiKeys.Endpoints"]
        APIKEYS_EF["Auth.ApiKeys.EF"]
    end

    subgraph Authorization
        AUTHZ["Authorization"]
        AUTHZ_EF["Authorization.EF"]
        AUTHZ_EP["Authorization.Endpoints"]
    end

    subgraph Caching
        CACHE["Caching"]
        CACHE_REDIS["Caching.Redis"]
        CACHE_HYB["Caching.Hybrid"]
    end

    subgraph Persistence
        PERS["Persistence"]
        PERS_MIG["Persistence.Migrations"]
        PERS_MIG_WOL["Persistence.Migrations.Wolverine"]
    end

    subgraph Wolverine
        WOL["Wolverine"]
        WOL_PG["Wolverine.Postgresql"]
        WOL_SQL["Wolverine.SqlServer"]
    end

    CORE --> Primitives
    CORE --> SEC
    CORE --> CACHE

    ENCR --> VAULT["Vault"]
    VAULT --> VAULT_HC["Vault.HashiCorp"]
    VAULT --> VAULT_AZ["Vault.Azure"]
    VAULT --> VAULT_AW["Vault.Aws"]
    TIMING --> GUIDS

    SEC --> JWT
    JWT --> KC
    JWT --> ENTRA
    SEC --> APIKEYS
    APIKEYS --> APIKEYS_EP
    APIKEYS --> APIKEYS_EF

    SEC --> AUTHZ
    CACHE --> AUTHZ
    AUTHZ --> AUTHZ_EF
    AUTHZ --> AUTHZ_EP

    CACHE --> CACHE_REDIS
    CACHE_REDIS --> CACHE_HYB

    GUIDS --> PERS
    SEC --> PERS
    EXC --> PERS
    PERS --> PERS_MIG
    PERS_MIG --> PERS_MIG_WOL

    SEC --> WOL
    WOL --> WOL_PG
    WOL --> WOL_SQL
    WOL --> PERS_MIG_WOL
    PERS --> WOL_PG
    PERS --> WOL_SQL

    style KC fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style ENTRA fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_HC fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_AZ fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_AW fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style CACHE_REDIS fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style WOL_PG fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style WOL_SQL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

### Utilities (flat dependencies)

| Package | Depends on |
|---------|------------|
| `Granit.Timing` | `Core` |
| `Granit.Security` | `Core` |
| `Granit.ExceptionHandling` | `Core` |
| `Granit.Observability` | `Core` |
| `Granit.MultiTenancy` | `Core` |
| `Granit.Privacy` | `Core` |
| `Granit.Cors` | `Core` |
| `Granit.Guids` | `Timing` |
| `Granit.Diagnostics` | `Timing` |
| `Granit.Validation` | `ExceptionHandling`, `Localization` |
| `Granit.Validation.Europe` | `Validation`, `Localization` |
| `Granit.Bulkhead` | `Core`, `ExceptionHandling`, `Features`, `Security` |
| `Granit.RateLimiting` | `Core`, `ExceptionHandling`, `Features`, `Security` |

### Identity

| Package | Depends on |
|---------|------------|
| `Granit.Identity` | `Querying` |
| `Granit.Identity.Keycloak` | `Identity` |
| `Granit.Identity.EntraId` | `Identity`, `Timing` |
| `Granit.Identity.EntityFrameworkCore` | `Identity`, `Persistence`, `Security` |
| `Granit.Identity.Endpoints` | `Identity`, `Authorization` |

### Localization

| Package | Depends on |
|---------|------------|
| `Granit.Localization` | `Core` |
| `Granit.Localization.EntityFrameworkCore` | `Localization`, `Persistence` |
| `Granit.Localization.Endpoints` | `Localization`, `Authorization` |
| `Granit.Localization.SourceGenerator` | none (source generator) |

### Configuration (Settings, Features, ReferenceData)

| Package | Depends on |
|---------|------------|
| `Granit.Settings` | `Caching`, `Encryption`, `Security` |
| `Granit.Settings.EntityFrameworkCore` | `Settings`, `Persistence` |
| `Granit.Settings.Endpoints` | `Settings`, `Authorization`, `Timing`, `Validation` |
| `Granit.Features` | `Caching`, `Localization` |
| `Granit.Features.EntityFrameworkCore` | `Features`, `Persistence` |
| `Granit.ReferenceData` | `Querying` |
| `Granit.ReferenceData.Endpoints` | `ReferenceData` |
| `Granit.ReferenceData.EntityFrameworkCore` | `ReferenceData`, `Persistence` |

### Web, API, and Webhooks

| Package | Depends on |
|---------|------------|
| `Granit.ApiVersioning` | `Core` |
| `Granit.ApiDocumentation` | `ApiVersioning`, `Security` |
| `Granit.Cookies` | `Timing` |
| `Granit.Cookies.Klaro` | `Cookies` |
| `Granit.Cookies.Endpoints` | `Cookies`, `Core` |
| `Granit.Idempotency` | `Caching`, `Security` |
| `Granit.Webhooks` | `Timing`, `Wolverine` |
| `Granit.Webhooks.EntityFrameworkCore` | `Webhooks`, `Persistence` |
| `Granit.Webhooks.Wolverine` | `Webhooks`, `Wolverine` |

### Storage and Imaging

| Package | Depends on |
|---------|------------|
| `Granit.BlobStorage` | `Guids` |
| `Granit.BlobStorage.S3` | `BlobStorage` |
| `Granit.BlobStorage.AzureBlob` | `BlobStorage` |
| `Granit.BlobStorage.FileSystem` | `BlobStorage` |
| `Granit.BlobStorage.Database` | `BlobStorage`, `Persistence` |
| `Granit.BlobStorage.Proxy` | `BlobStorage` |
| `Granit.BlobStorage.EntityFrameworkCore` | `BlobStorage`, `Persistence` |
| `Granit.Imaging` | `Core` |
| `Granit.Imaging.MagickNet` | `Imaging` |

## Messaging and notification layer

### Notifications

Fan-out multi-channel engine with Brevo as a unified aggregator across Email, SMS,
and WhatsApp.

```mermaid
flowchart LR
    NOTIF["Notifications"] --> NOTIF_EP["Notifications.Endpoints"]
    NOTIF --> NOTIF_EF["Notifications.EF"]
    NOTIF --> NOTIF_WOL["Notifications.Wolverine"]

    NOTIF --> NOTIF_EMAIL["Notifications.Email"]
    NOTIF_EMAIL --> NOTIF_SMTP["Notifications.Email.Smtp"]

    NOTIF --> NOTIF_SMS["Notifications.Sms"]
    NOTIF --> NOTIF_WA["Notifications.WhatsApp"]
    NOTIF --> NOTIF_PUSH["Notifications.WebPush"]
    NOTIF --> NOTIF_SR["Notifications.SignalR"]
    NOTIF --> NOTIF_SSE["Notifications.Sse"]
    NOTIF --> NOTIF_ZULIP["Notifications.Zulip"]

    NOTIF --> NOTIF_MP["Notifications.MobilePush"]
    NOTIF_MP --> NOTIF_FCM["Notifications.MobilePush.GoogleFcm"]

    NOTIF_EMAIL --> NOTIF_BREVO["Notifications.Brevo"]
    NOTIF_SMS --> NOTIF_BREVO
    NOTIF_WA --> NOTIF_BREVO

    NOTIF_EMAIL --> NOTIF_ACS_EMAIL["Notifications.Email.AzureCommunicationServices"]
    NOTIF_SMS --> NOTIF_ACS_SMS["Notifications.Sms.AzureCommunicationServices"]
    NOTIF_MP --> NOTIF_ANH["Notifications.MobilePush.AzureNotificationHubs"]

    style NOTIF_EMAIL fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_SMS fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_WA fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_MP fill:#e3f2fd,stroke:#1976d2,color:#0d47a1

    style NOTIF_SMTP fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_BREVO fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ACS_EMAIL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ACS_SMS fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ANH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SR fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SSE fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ZULIP fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_PUSH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_FCM fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

### Templating and Document Generation

Template engine (Scriban) with document rendering pipeline (HTML-to-PDF, Excel).

```mermaid
flowchart LR
    TMPL["Templating"] --> SCRIBAN["Templating.Scriban"]
    TMPL --> TMPL_EF["Templating.EF"]
    TMPL --> TMPL_EP["Templating.Endpoints"]
    TMPL --> TMPL_WF["Templating.Workflow"]

    TMPL --> DOCGEN["DocumentGeneration"]
    DOCGEN --> DOCGEN_PDF["DocumentGeneration.Pdf"]
    TMPL --> DOCGEN_EXCEL["DocumentGeneration.Excel"]

    style SCRIBAN fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style DOCGEN_PDF fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style DOCGEN_EXCEL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

| Package | Depends on |
|---------|------------|
| `Granit.Templating` | `Timing` |
| `Granit.Templating.Scriban` | `Templating` |
| `Granit.Templating.EntityFrameworkCore` | `Templating` |
| `Granit.Templating.Endpoints` | `Templating`, `Authorization` |
| `Granit.Templating.Workflow` | `Templating`, `Workflow` |
| `Granit.DocumentGeneration` | `Templating` |
| `Granit.DocumentGeneration.Pdf` | `DocumentGeneration` |
| `Granit.DocumentGeneration.Excel` | `Templating` |

### Workflow and Timeline

These two domains share cross-module dependencies with Notifications and Identity.

| Package | Depends on |
|---------|------------|
| `Granit.Workflow` | `Timing` |
| `Granit.Workflow.EntityFrameworkCore` | `Workflow`, `Persistence` |
| `Granit.Workflow.Endpoints` | `Workflow`, `Authorization` |
| `Granit.Workflow.Notifications` | `Workflow`, `Authorization`, `Identity`, `Notifications` |
| `Granit.Timeline` | `Guids`, `Security` |
| `Granit.Timeline.EntityFrameworkCore` | `Timeline`, `Persistence` |
| `Granit.Timeline.Endpoints` | `Timeline`, `Authorization` |
| `Granit.Timeline.Notifications` | `Timeline`, `Notifications` |

### Background Jobs

| Package | Depends on |
|---------|------------|
| `Granit.BackgroundJobs` | `Timing`, `Wolverine` |
| `Granit.BackgroundJobs.EntityFrameworkCore` | `BackgroundJobs` |
| `Granit.BackgroundJobs.Endpoints` | `BackgroundJobs`, `Authorization` |
| `Granit.BackgroundJobs.Wolverine` | `BackgroundJobs`, `Wolverine` |

### Querying

| Package | Depends on |
|---------|------------|
| `Granit.Querying` | `Core` |
| `Granit.Querying.Endpoints` | `Querying`, `Authorization` |
| `Granit.Querying.EntityFrameworkCore` | `Querying`, `Persistence` |

### DataExchange

Import/export pipeline with format adapters (CSV, Excel) and async processing via Wolverine.

```mermaid
flowchart LR
    DX["DataExchange"] --> DX_CSV["DataExchange.Csv"]
    DX --> DX_EXCEL["DataExchange.Excel"]
    DX --> DX_EF["DataExchange.EF"]
    DX --> DX_EP["DataExchange.Endpoints"]
    DX --> DX_WOL["DataExchange.Wolverine"]

    style DX_CSV fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style DX_EXCEL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

| Package | Depends on |
|---------|------------|
| `Granit.DataExchange` | `Querying`, `Timing`, `Validation` |
| `Granit.DataExchange.Csv` | `DataExchange` |
| `Granit.DataExchange.Excel` | `DataExchange` |
| `Granit.DataExchange.EntityFrameworkCore` | `DataExchange`, `Persistence` |
| `Granit.DataExchange.Endpoints` | `DataExchange`, `Authorization` |
| `Granit.DataExchange.Wolverine` | `DataExchange`, `Wolverine` |

### Analyzers

| Package | Depends on |
|---------|------------|
| `Granit.Analyzers` | none (Roslyn analyzer) |
| `Granit.Analyzers.CodeFixes` | `Analyzers` |

## Soft dependencies

Several core interfaces live in `Granit.Core` rather than in their dedicated module.
This allows any package to consume them without taking a hard reference to the
implementing module. The implementing module registers the real service; without it, a
null-object default is used.

| Interface | Declared in | Implemented by | Default behavior |
|-----------|-------------|----------------|------------------|
| `ICurrentTenant` | `Granit.Core.MultiTenancy` | `Granit.MultiTenancy` | `NullTenantContext` (`IsAvailable = false`) |
| `IDataFilter` | `Granit.Core.DataFiltering` | `Granit.Persistence` | No-op (all data visible) |

Modules that access `ICurrentTenant` use `using Granit.Core.MultiTenancy;` and check
`IsAvailable` before reading `Id`. They do NOT declare `[DependsOn(typeof(GranitMultiTenancyModule))]`
or add a `ProjectReference` to `Granit.MultiTenancy`. The only exception is modules that
must enforce strict tenant isolation (e.g., BlobStorage for GDPR compliance).

## Bundle composition

Five meta-packages provide curated sets of modules for common application profiles.
Bundles contain no code -- they are `ProjectReference`-only `.csproj` files.

### Granit.Bundle.Essentials

Minimal API foundation.

| Included package |
|------------------|
| `Granit.Core` |
| `Granit.Timing` |
| `Granit.Guids` |
| `Granit.Security` |
| `Granit.Validation` |
| `Granit.Persistence` |
| `Granit.Observability` |
| `Granit.ExceptionHandling` |
| `Granit.Diagnostics` |

### Granit.Bundle.Api

Complete REST API. Includes everything in `Bundle.Essentials` plus:

| Included package |
|------------------|
| `Granit.ApiVersioning` |
| `Granit.ApiDocumentation` |
| `Granit.Cors` |
| `Granit.Idempotency` |
| `Granit.Localization` |
| `Granit.Localization.EntityFrameworkCore` |
| `Granit.Caching` |

### Granit.Bundle.Documents

Templating and document generation pipeline.

| Included package |
|------------------|
| `Granit.Templating` |
| `Granit.Templating.Scriban` |
| `Granit.Templating.EntityFrameworkCore` |
| `Granit.DocumentGeneration` |
| `Granit.DocumentGeneration.Pdf` |
| `Granit.DocumentGeneration.Excel` |

### Granit.Bundle.Notifications

Multi-channel notification engine with default channels.

| Included package |
|------------------|
| `Granit.Notifications` |
| `Granit.Notifications.EntityFrameworkCore` |
| `Granit.Notifications.Endpoints` |
| `Granit.Notifications.Email` |
| `Granit.Notifications.Email.Smtp` |
| `Granit.Notifications.SignalR` |

### Granit.Bundle.SaaS

Multi-tenant SaaS extensions.

| Included package |
|------------------|
| `Granit.MultiTenancy` |
| `Granit.Features` |
| `Granit.Features.EntityFrameworkCore` |
| `Granit.RateLimiting` |
| `Granit.Bulkhead` |

## Dependency rules

These invariants are enforced by `Granit.ArchitectureTests` and apply to every package
in the repository.

1. **Core depends on nothing.** It is the root of the entire graph.

2. **Zero circular references.** The graph is a strict DAG (directed acyclic graph).
   The build will fail if a cycle is introduced.

3. **One project = one NuGet package.** The namespace matches the project name. No
   assembly contains types from another package's namespace.

4. **Functional packages never reference `*.EntityFrameworkCore` packages.** Abstractions
   live in the base package; EF Core implementations live in the `*.EntityFrameworkCore`
   companion. This keeps the base package ORM-agnostic.

5. **`*.Endpoints` packages depend on `Granit.Authorization`.** Every endpoint package
   uses `RequireAuthorization()` or policy-based authorization. This is a systematic
   pattern omitted from the diagrams for readability.

6. **Wolverine is the sole message bus.** All asynchronous processing flows through
   Wolverine: Notifications, Webhooks, BackgroundJobs, DataExchange.Wolverine,
   Persistence.Migrations.Wolverine.

7. **`Persistence.Migrations` is decoupled from Wolverine.** Batch dispatch is abstracted
   via `IMigrationBatchDispatcher` (Channel-based by default, Wolverine optional via
   `Granit.Persistence.Migrations.Wolverine`).

8. **Multi-tenancy is a soft dependency.** Modules use `ICurrentTenant` from
   `Granit.Core.MultiTenancy` without referencing `Granit.MultiTenancy`. Hard dependency
   is reserved for strict tenant isolation (BlobStorage, GDPR).

## Graph properties

- **%%PACKAGE_COUNT%% source packages**, zero circular dependencies
- **Maximum depth**: 5 levels (e.g., Core to Security to Wolverine to Notifications to
  Email to Smtp, or Core to Timing to Notifications to MobilePush to MobilePush.GoogleFcm)
- **Leaf packages**: `*.EntityFrameworkCore` and `*.S3` packages are almost always leaves
- **Root packages with no dependencies**: `Granit.Core`, `Granit.Analyzers`,
  `Granit.Localization.SourceGenerator`
- **5 bundle meta-packages**: Essentials, Api, Documents, Notifications, SaaS

## Intentional design decisions

Several patterns in the dependency graph may look like anomalies during an audit but
are deliberate choices.

### Isolated DbContext packages

Seven `*.EntityFrameworkCore` packages use an autonomous `DbContext` via
`IDbContextFactory` instead of the application `DbContext` managed by
`Granit.Persistence`: Authorization, BackgroundJobs, Localization, Features, Settings,
Webhooks, BlobStorage.

These modules manage infrastructure data (not business entities), use `IDbContextFactory`
for thread safety with parallel Wolverine handlers, and do not need
`AuditedEntityInterceptor` or `SoftDeleteInterceptor`.

### Caching.Hybrid depends on StackExchangeRedis

`HybridCache` (.NET 9+) requires a distributed L2 backend. Redis is the only backend
supported by the Granit stack, making this a structural dependency.

### DataExchange depends on Querying

The coupling is export-only: `DataExchange` reads `QueryDefinition` metadata to generate
tabular exports (columns, filters, sort). The import pipeline uses no Querying types.

### DocumentGeneration.Excel depends on Templating

The XLSX package (ClosedXML) bypasses the HTML-to-render pipeline because XLSX is a
binary format. It references `Templating` for `ITextTemplateRenderer` (Scriban cell
rendering) and the polymorphic `RenderedContent` types.

### Notifications.Endpoints without RBAC

All notification endpoints are per-user self-service operations (inbox, preferences,
subscriptions). Each endpoint filters by `GetUserId(user)` and cannot access another
user's data. `.RequireAuthorization()` without a RBAC policy is sufficient.
