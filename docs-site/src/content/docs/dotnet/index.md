---
title: Reference
description: Complete reference documentation for all 97 Granit packages
sidebar:
  order: 0
---

This section provides detailed reference documentation for every Granit module.

Each module page documents the full package family (abstractions, providers,
EF Core integration, endpoints), configuration options, public API surface,
and provider comparison.

## Module categories

| Category | Modules | Description |
|----------|---------|-------------|
| Core & Utilities | [Core & Utilities](./modules/core/) | Foundation types, module system, Timing, Guids, Validation |
| Security | [Security & Identity](./modules/security/), [Privacy](./modules/privacy/), [Vault & Encryption](./modules/vault-encryption/) | Authentication, authorization, encryption, GDPR |
| Identity | [Identity](./modules/identity/) | Keycloak/EntraID/Cognito integration, user cache |
| Data & Persistence | [Persistence](./modules/persistence/), [Caching](./modules/caching/), [Multi-Tenancy](./modules/multi-tenancy/) | EF Core interceptors, HybridCache, tenant isolation |
| Settings & Features | [Settings & Features](./modules/settings-features/) | Application settings, feature flags, reference data |
| API & Web | [API & Web](./modules/api-web/) | Versioning, OpenAPI docs, idempotency, CORS, cookies |
| Messaging | [Wolverine](./modules/wolverine/), [Webhooks](./modules/webhooks/), [Notifications](./modules/notifications/) | Message bus, outbox, webhooks, 6-channel notifications |
| Audit | [Timeline](./modules/timeline/) | Entity activity stream (chatter), follow/notify |
| Documents | [Templating & DocumentGeneration](./modules/templating/) | Scriban templates, HTML-to-PDF, Excel generation |
| Data Exchange | [DataExchange](./modules/data-exchange/) | Import pipeline (CSV, Excel), export presets |
| Workflow | [Workflow](./modules/workflow/) | FSM engine, publication lifecycle |
| Diagnostics | [Observability & Diagnostics](./modules/observability/) | Serilog, OpenTelemetry, health checks |
| Storage | [BlobStorage & Imaging](./modules/blob-storage/) | Multi-provider storage (S3, Azure, FileSystem, Database), image processing |
| Scheduling | [BackgroundJobs](./modules/background-jobs/) | Recurring and delayed jobs (Wolverine + Cronos) |
| Localization | [Localization](./modules/localization/) | i18n (17 cultures), source-generated keys |

## Cross-cutting references

- [Configuration Keys](./configuration-keys/) -- all appsettings sections and Options classes
- [HTTP Conventions](./http-conventions/) -- status codes, Problem Details, DTO naming
- [Dependency Graph](./dependency-graph/) -- package relationships and module dependencies
- [Cloud Providers](./cloud-providers/) -- packages by cloud provider (AWS, Azure, Google Cloud)
- [Provider Compatibility](./provider-compatibility/) -- database, cache, storage support matrix
- [Tech Stack](./tech-stack/) -- third-party libraries, licenses, and ADR links
