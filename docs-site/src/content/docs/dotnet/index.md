---
title: Backend (.NET)
description: Granit .NET framework — modular, production-ready modules for authentication, persistence, messaging, AI, and GDPR/ISO 27001 compliance.
sidebar:
  label: Overview
  order: 0
---

Granit is a modular .NET 10 framework providing production-ready building blocks
for enterprise applications. Each module follows the same layered anatomy
(abstractions, EF Core, endpoints, providers) and wires into the
dependency injection system via `[DependsOn]`.

## Module groups

| Group | Modules | Purpose |
| ----- | ------- | ------- |
| [Core](/dotnet/core/module-system/) | Module System, Utilities, Analyzers | Foundation types, module lifecycle, Roslyn analyzers |
| [Data](/dotnet/data/persistence/) | Persistence, Caching, Storage, Querying, Reference Data | EF Core interceptors, HybridCache, multi-provider storage |
| [Security & Compliance](/dotnet/security/authentication/) | Authentication, Authorization, Security, Identity, Privacy, Vault & Encryption | JWT Bearer, RBAC, GDPR, key management |
| [API](/dotnet/api/api/) | API & Web, Webhooks | Versioning, OpenAPI, idempotency, HMAC-signed webhooks |
| [Infrastructure](/dotnet/infrastructure/wolverine/) | Wolverine, Notifications, Background Jobs, Localization, Settings, Multi-Tenancy, Features | Message bus, 6-channel notifications, i18n (17 cultures), time zones, SaaS enablement |
| [Observability](/dotnet/observability/observability-otlp/) | Observability & Diagnostics | Serilog, OpenTelemetry, health checks |
| [Business Features](/dotnet/business/workflow/) | Workflow, Data Exchange, Templating, Timeline | FSM engine, import/export, PDF generation, activity stream |

## AI

Granit.AI adds provider-agnostic LLM capabilities (OpenAI, Azure, Anthropic, Ollama)
via Microsoft.Extensions.AI. See the [AI section](/dotnet/ai/) for setup, NLQ,
semantic search, document extraction, and more.

## Architecture

- [HTTP Conventions](/dotnet/architecture/http-conventions/) — status codes, Problem Details, DTO naming
- [Dependency Graph](/dotnet/architecture/dependency-graph/) — package relationships
- [Tech Stack](/dotnet/architecture/tech-stack/) — third-party libraries and licenses
- [Design Patterns](/dotnet/architecture/patterns/) — 43 patterns documented
- [ADRs](/dotnet/architecture/adr/) — architecture decision records

## Reference

- [Configuration Keys](/dotnet/configuration-keys/) — all appsettings sections and Options classes
- [Cloud Providers](/dotnet/cloud-providers/) — packages by cloud provider (AWS, Azure, Google Cloud)
- [Provider Compatibility](/dotnet/provider-compatibility/) — database, cache, storage support matrix
