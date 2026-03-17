---
title: Dependency Graph
description: Package dependency visualization and module relationship map for all Granit packages
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
    classDef core     fill:#0ea5e9,stroke:#0284c7,color:#fff
    classDef dataLyr  fill:#3b82f6,stroke:#2563eb,color:#fff
    classDef security fill:#6366f1,stroke:#4f46e5,color:#fff
    classDef api      fill:#ec4899,stroke:#db2777,color:#fff
    classDef infra    fill:#7c3aed,stroke:#6d28d9,color:#fff
    classDef business fill:#e879f9,stroke:#c026d3,color:#fff
    classDef ai       fill:#f59e0b,stroke:#b45309,color:#fff

    CORE["Core (1)"]:::core

    subgraph Foundation
        UTILS["Utilities (14)"]:::core
        SEC["Security (18)"]:::security
        CACHE["Caching (3)"]:::dataLyr
        IDENT["Identity (7)"]:::security
    end

    subgraph Data
        PERS["Persistence (3)"]:::dataLyr
        STORAGE["Storage (10)"]:::dataLyr
    end

    subgraph Infrastructure
        WOL["Wolverine (3)"]:::infra
        LOC["Localization (4)"]:::infra
        CONFIG["Configuration (8)"]:::infra
        JOBS["Background Jobs (4)"]:::infra
        NOTIF["Notifications (28)"]:::infra
    end

    subgraph API["API & Http"]
        WEB["Web, API, Webhooks (9)"]:::api
    end

    subgraph Business
        TMPL["Templating (8)"]:::business
        QRY["Querying (3)"]:::business
        DX["DataExchange (6)"]:::business
        WF["Workflow (4)"]:::business
        TL["Timeline (4)"]:::business
    end

    AI["AI (21)"]:::ai
    ANLZ["Analyzers (2)"]:::core

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

    CORE --> AI
    PERS --> AI
    SEC --> AI
```

### Domain composition

| Domain | Packages |
|--------|----------|
| Utilities | Timing, Guids, Diagnostics, Validation, Validation.Europe, Validation.NorthAmerica, Validation.UnitedKingdom, Http.ExceptionHandling, Observability, MultiTenancy, Privacy, Cors, Bulkhead, RateLimiting |
| Identity | Identity, Identity.Keycloak, Identity.EntraId, Identity.Cognito, Identity.GoogleCloud, Identity.EntityFrameworkCore, Identity.Endpoints |
| Security | Security, Encryption, Vault, Vault.HashiCorp, Vault.Azure, Vault.Aws, Vault.GoogleCloud, Authentication.JwtBearer, Authentication.Keycloak, Authentication.EntraId, Authentication.Cognito, Authentication.GoogleCloud, Authentication.ApiKeys (3), Authorization, Authorization.EntityFrameworkCore, Authorization.Endpoints |
| Configuration | Settings (3), Features (2), ReferenceData (3) |
| Web, API, and Webhooks | ApiVersioning, ApiDocumentation, Cookies, Cookies.Klaro, Cookies.Endpoints, Http.Idempotency, Webhooks (3) |
| Storage | BlobStorage (8 incl. GoogleCloud), Imaging (2) |
| Background Jobs | BackgroundJobs (4) |
| Localization | Localization, Localization.EntityFrameworkCore, Localization.Endpoints, Localization.SourceGenerator |
| Templating | Templating, Templating.Scriban, Templating.EntityFrameworkCore, Templating.Endpoints, Templating.Workflow, DocumentGeneration, DocumentGeneration.Pdf, DocumentGeneration.Excel |
| Notifications | Notifications, Notifications.EntityFrameworkCore, Notifications.Endpoints, Notifications.Wolverine, Email (7 providers), Sms (3 providers), WhatsApp, WebPush, SignalR, Sse, Zulip, Brevo, Twilio, MobilePush (4 providers) |
| Workflow | Workflow, Workflow.EntityFrameworkCore, Workflow.Endpoints, Workflow.Notifications |
| Timeline | Timeline, Timeline.EntityFrameworkCore, Timeline.Endpoints, Timeline.Notifications |
| DataExchange | DataExchange, DataExchange.Csv, DataExchange.Excel, DataExchange.EntityFrameworkCore, DataExchange.Endpoints, DataExchange.Wolverine |
| AI | AI, AI.OpenAI, AI.AzureOpenAI, AI.Anthropic, AI.Ollama, AI.VectorData, AI.Extraction, AI.EntityFrameworkCore, and 13 cross-cutting `*.AI` packages |

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
        JWT["Authentication.JwtBearer"]
        KC["Authentication.Keycloak"]
        ENTRA["Authentication.EntraId"]
        COGNITO["Authentication.Cognito"]
        GC_AUTH["Authentication.GoogleCloud"]
        APIKEYS["Authentication.ApiKeys"]
        APIKEYS_EP["Authentication.ApiKeys.Endpoints"]
        APIKEYS_EF["Authentication.ApiKeys.EntityFrameworkCore"]
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
    VAULT --> VAULT_GC["Vault.GoogleCloud"]
    TIMING --> GUIDS

    SEC --> JWT
    JWT --> KC
    JWT --> ENTRA
    JWT --> COGNITO
    JWT --> GC_AUTH
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
    style COGNITO fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style GC_AUTH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_HC fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_AZ fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_AW fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style VAULT_GC fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style CACHE_REDIS fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style WOL_PG fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style WOL_SQL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

### Utilities (flat dependencies)

| Package | Depends on |
|---------|------------|
| `Granit.Timing` | `Core` |
| `Granit.Security` | `Core` |
| `Granit.Http.ExceptionHandling` | `Core` |
| `Granit.Observability` | `Core` |
| `Granit.MultiTenancy` | `Core` |
| `Granit.Privacy` | `Core` |
| `Granit.Http.Cors` | `Core` |
| `Granit.Guids` | `Timing` |
| `Granit.Diagnostics` | `Timing` |
| `Granit.Validation` | `ExceptionHandling`, `Localization` |
| `Granit.Validation.Europe` | `Validation`, `Localization` |
| `Granit.Validation.NorthAmerica` | `Validation`, `Localization` |
| `Granit.Validation.UnitedKingdom` | `Validation`, `Localization` |
| `Granit.Http.Bulkhead` | `Core`, `ExceptionHandling`, `Features`, `Security` |
| `Granit.RateLimiting` | `Core`, `ExceptionHandling`, `Features`, `Security` |

### Identity

| Package | Depends on |
|---------|------------|
| `Granit.Identity` | `Querying` |
| `Granit.Identity.Keycloak` | `Identity` |
| `Granit.Identity.EntraId` | `Identity`, `Timing` |
| `Granit.Identity.Cognito` | `Identity` |
| `Granit.Identity.GoogleCloud` | `Identity` |
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
| `Granit.Http.ApiVersioning` | `Core` |
| `Granit.Http.ApiDocumentation` | `ApiVersioning`, `Security` |
| `Granit.Http.Cookies` | `Timing` |
| `Granit.Http.Cookies.Klaro` | `Cookies` |
| `Granit.Http.Cookies.Endpoints` | `Cookies`, `Core` |
| `Granit.Http.Idempotency` | `Caching`, `Security` |
| `Granit.Webhooks` | `Timing`, `Wolverine` |
| `Granit.Webhooks.EntityFrameworkCore` | `Webhooks`, `Persistence` |
| `Granit.Webhooks.Wolverine` | `Webhooks`, `Wolverine` |

### Storage and Imaging

| Package | Depends on |
|---------|------------|
| `Granit.BlobStorage` | `Guids` |
| `Granit.BlobStorage.S3` | `BlobStorage` |
| `Granit.BlobStorage.AzureBlob` | `BlobStorage` |
| `Granit.BlobStorage.GoogleCloud` | `BlobStorage` |
| `Granit.BlobStorage.FileSystem` | `BlobStorage` |
| `Granit.BlobStorage.DbStore` | `BlobStorage`, `Persistence` |
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
    NOTIF_EMAIL --> NOTIF_ACS_EMAIL["Notifications.Email.AzureCommunicationServices"]
    NOTIF_EMAIL --> NOTIF_AWSSES["Notifications.Email.AwsSes"]
    NOTIF_EMAIL --> NOTIF_SENDGRID["Notifications.Email.SendGrid"]
    NOTIF_EMAIL --> NOTIF_SCALEWAY["Notifications.Email.Scaleway"]

    NOTIF --> NOTIF_SMS["Notifications.Sms"]
    NOTIF_SMS --> NOTIF_ACS_SMS["Notifications.Sms.AzureCommunicationServices"]
    NOTIF_SMS --> NOTIF_AWSSNS["Notifications.Sms.AwsSns"]
    NOTIF_SMS --> NOTIF_TWILIO["Notifications.Twilio"]

    NOTIF --> NOTIF_WA["Notifications.WhatsApp"]
    NOTIF --> NOTIF_PUSH["Notifications.WebPush"]
    NOTIF --> NOTIF_SR["Notifications.SignalR"]
    NOTIF --> NOTIF_SSE["Notifications.Sse"]
    NOTIF --> NOTIF_ZULIP["Notifications.Zulip"]

    NOTIF_EMAIL --> NOTIF_BREVO["Notifications.Brevo"]
    NOTIF_SMS --> NOTIF_BREVO
    NOTIF_WA --> NOTIF_BREVO

    NOTIF --> NOTIF_MP["Notifications.MobilePush"]
    NOTIF_MP --> NOTIF_FCM["Notifications.MobilePush.GoogleFcm"]
    NOTIF_MP --> NOTIF_ANH["Notifications.MobilePush.AzureNotificationHubs"]
    NOTIF_MP --> NOTIF_AWSSNS_PUSH["Notifications.MobilePush.AwsSns"]

    style NOTIF_EMAIL fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_SMS fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_WA fill:#e3f2fd,stroke:#1976d2,color:#0d47a1
    style NOTIF_MP fill:#e3f2fd,stroke:#1976d2,color:#0d47a1

    style NOTIF_SMTP fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ACS_EMAIL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_AWSSES fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SENDGRID fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SCALEWAY fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ACS_SMS fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_AWSSNS fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_TWILIO fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_BREVO fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SR fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_SSE fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ZULIP fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_PUSH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_FCM fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_ANH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style NOTIF_AWSSNS_PUSH fill:#e8f5e9,stroke:#43a047,color:#1b5e20
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

### AI

Provider-agnostic AI layer built on `Microsoft.Extensions.AI`. Core provider packages plus
thirteen cross-cutting `*.AI` packages that add AI capabilities to existing modules.

```mermaid
flowchart LR
    AI["AI"] --> AI_EF["AI.EntityFrameworkCore"]
    AI --> AI_OAI["AI.OpenAI"]
    AI --> AI_AOAI["AI.AzureOpenAI"]
    AI --> AI_ANT["AI.Anthropic"]
    AI --> AI_OLL["AI.Ollama"]
    AI --> AI_VEC["AI.VectorData"]
    AI --> AI_EXT["AI.Extraction"]

    AI --> AUTH_AI["Authorization.AI"]
    AI --> BLOB_AI["BlobStorage.AI"]
    AI --> DX_AI["DataExchange.AI"]
    AI --> IMG_AI["Imaging.AI"]
    AI --> LOC_AI["Localization.AI"]
    AI --> NOTIF_AI["Notifications.AI"]
    AI --> OBS_AI["Observability.AI"]
    AI --> PRIV_AI["Privacy.AI"]
    AI --> QRY_AI["Querying.AI"]
    AI --> TMPL_AI["Templating.AI"]
    AI --> TL_AI["Timeline.AI"]
    AI --> VAL_AI["Validation.AI"]
    AI --> WF_AI["Workflow.AI"]

    style AI_OAI fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style AI_AOAI fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style AI_ANT fill:#e8f5e9,stroke:#43a047,color:#1b5e20
    style AI_OLL fill:#e8f5e9,stroke:#43a047,color:#1b5e20
```

| Package | Depends on |
|---------|------------|
| `Granit.AI` | `Core`, `Guids` |
| `Granit.AI.EntityFrameworkCore` | `AI`, `Persistence` |
| `Granit.AI.OpenAI` | `AI` |
| `Granit.AI.AzureOpenAI` | `AI` |
| `Granit.AI.Anthropic` | `AI` |
| `Granit.AI.Ollama` | `AI` |
| `Granit.AI.VectorData` | `AI` |
| `Granit.AI.Extraction` | `AI` |
| `Granit.Authorization.AI` | `AI`, `Authorization` |
| `Granit.BlobStorage.AI` | `AI`, `BlobStorage` |
| `Granit.DataExchange.AI` | `AI`, `DataExchange` |
| `Granit.Imaging.AI` | `AI`, `Imaging` |
| `Granit.Localization.AI` | `AI`, `Localization` |
| `Granit.Notifications.AI` | `AI`, `Notifications` |
| `Granit.Observability.AI` | `AI`, `Observability` |
| `Granit.Privacy.AI` | `AI`, `Privacy` |
| `Granit.Querying.AI` | `AI`, `Querying` |
| `Granit.Templating.AI` | `AI`, `Templating` |
| `Granit.Timeline.AI` | `AI`, `Timeline` |
| `Granit.Validation.AI` | `AI`, `Validation` |
| `Granit.Workflow.AI` | `AI`, `Workflow` |

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
| `Granit.Http.ExceptionHandling` |
| `Granit.Diagnostics` |

### Granit.Bundle.Api

Complete REST API. Includes everything in `Bundle.Essentials` plus:

| Included package |
|------------------|
| `Granit.Http.ApiVersioning` |
| `Granit.Http.ApiDocumentation` |
| `Granit.Http.Cors` |
| `Granit.Http.Idempotency` |
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
| `Granit.Http.Bulkhead` |

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
- **Maximum depth**: 5 levels (e.g., Core → Security → Wolverine → Notifications →
  Email → Smtp, or Core → AI → AI.OpenAI → cross-cutting *.AI packages)
- **Leaf packages**: `*.EntityFrameworkCore`, `*.S3`, `*.GoogleCloud`, `*.AI` packages
  are almost always leaves
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
