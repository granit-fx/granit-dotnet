<p align="center">
  <img src="docs-site/src/assets/granit-icon.svg" alt="granit" width="200" />
</p>

<p align="center">
  <strong>Solid by design. Modular by nature.</strong>
</p>

<p align="center">
  .NET 10 · C# 14 · EF Core 10 · CQRS · Modular Monolith
</p>

<p align="center">
  <a href="https://github.com/granit-fx/granit-dotnet/actions/workflows/ci.yml"><img src="https://github.com/granit-fx/granit-dotnet/actions/workflows/ci.yml/badge.svg?branch=develop" alt="CI"></a>
  <a href="https://sonarcloud.io/summary/new_code?id=granit-fx_granit-dotnet"><img src="https://sonarcloud.io/api/project_badges/measure?project=granit-fx_granit-dotnet&metric=alert_status" alt="Quality Gate Status"></a>
  <a href="https://sonarcloud.io/summary/new_code?id=granit-fx_granit-dotnet"><img src="https://sonarcloud.io/api/project_badges/measure?project=granit-fx_granit-dotnet&metric=coverage" alt="Coverage"></a>
  <a href="https://github.com/granit-fx/granit-dotnet/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-blue" alt="License"></a>
  <a href="https://www.bestpractices.dev/projects/12585"><img src="https://www.bestpractices.dev/projects/12585/badge" alt="OpenSSF Best Practices"></a>
</p>

---

Granit is a rock-solid, production-ready modular framework for .NET and React.
Built as a Modular Monolith with zero compromises on Developer Experience.
It provides **273 NuGet packages** organized as independent modules,
compliant with **GDPR/ISO 27001** requirements.

**📖 Full documentation: [granit-fx.dev](https://granit-fx.dev)**

## Features

| Domain | What Granit provides |
| --- | --- |
| **Core & Modularity** | Self-configuring module system, topological dependency sorting, timing, GUID generation |
| **Security** | JWT Bearer authentication (Keycloak, EntraID, Cognito, Google), API keys, DPoP, OpenIddict |
| **Authorization** | Fine-grained RBAC, permission definition providers, AI-assisted policy evaluation |
| **Identity** | Identity provider abstractions, user cache (cache-aside, login-time sync, GDPR) |
| **BFF** | Backend for Frontend with YARP reverse proxy, session management, OpenID Connect |
| **Persistence** | EF Core interceptors: audit trail (3 years), GDPR soft delete, multi-tenancy, settings, features |
| **Multi-tenancy** | Schema or database isolation, automatic resolution, transparent filtering |
| **Caching** | FusionCache (L1+L2+backplane, fail-safe, eager refresh), Redis, AES-256 encryption |
| **Query Engine** | Dynamic filtering, sorting, pagination over EF Core with OpenAPI schema generation |
| **Reference Data** | Versioned lookup tables, import/export, tenant-scoped overrides |
| **Observability** | Structured logging + distributed tracing → OTLP, health checks, per-module metrics |
| **Diagnostics** | `ActivitySource` per module, `IMeterFactory`-based metrics, `GranitActivitySourceRegistry` |
| **Messaging** | Wolverine transactional outbox, domain events, integration events (ETO) |
| **Background Jobs** | Recurring cron jobs (Wolverine scheduling), dashboard endpoints, history tracking |
| **Notifications** | 6 channels (email, SMS, push, in-app, Teams, Slack), templated content |
| **Webhooks** | HMAC-SHA256 signed delivery, retry policies, subscription management |
| **API** | Versioning, OpenAPI 3.1 Scalar, Stripe-style idempotency, ProblemDetails, CORS |
| **Rate Limiting** | Per-endpoint and per-tenant rate limiting with sliding window policies |
| **Storage & Imaging** | S3-compatible blob storage, pre-signed URLs, Crypto-Shredding, image processing |
| **Documents** | Template engine (Scriban), HTML→PDF rendering, Excel generation |
| **Data Exchange** | Import (Extract→Map→Validate→Execute), Export (tabular Excel/CSV with presets) |
| **Workflow** | FSM engine, publication lifecycle, approval routing |
| **Timeline** | Activity stream, audit-friendly event history, per-entity timeline |
| **Localization** | i18n (18 cultures), override store, source-generated keys |
| **Settings** | Dynamic key-value settings, tenant-scoped, typed access, change auditing |
| **SaaS** | Feature flags per commercial plan, quotas, Default → Plan → Tenant resolution |
| **Privacy & Encryption** | GDPR pseudonymization, right to erasure, Vault Transit encryption, key rotation |
| **Auditing** | Configuration change tracking, entity-level audit log, 3-year retention |
| **AI** | Provider-agnostic LLM (OpenAI, Azure, Anthropic, Ollama), NLQ, semantic search, extraction |
| **MCP** | Model Context Protocol server integration, SDK-native auth and filters |
| **Testing** | xUnit helpers, Testcontainers, Bogus fakers, architecture tests (ArchUnitNET) |
| **Quality** | Embedded Roslyn analyzers, FluentValidation (VAT, SIREN, NISS), OpenAPI schema enrichment |

## Quick start

```bash
# Add the foundation package to your project
dotnet add package Granit

# Add the modules you need
dotnet add package Granit.Persistence
dotnet add package Granit.Identity
dotnet add package Granit.Observability
```

```csharp
// Program.cs
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

await builder.AddGranitAsync<MyAppModule>();

WebApplication app = builder.Build();
app.Run();
```

```csharp
// MyAppModule.cs
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitIdentityModule),
    typeof(GranitObservabilityModule))]
public sealed class MyAppModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Granit modules are already configured automatically.
        // Add your application-specific configuration here.
    }
}
```

## Documentation

| Section | Content |
| --- | --- |
| [Getting Started](https://granit-fx.dev/dotnet/getting-started/) | Build a working Granit API in under 5 minutes |
| [Architecture](https://granit-fx.dev/dotnet/architecture/) | ADRs, patterns, module system, DDD |
| [Core](https://granit-fx.dev/dotnet/core/) | Module system, validation, diagnostics, analyzers |
| [Security](https://granit-fx.dev/dotnet/security/) | Authentication, authorization, encryption, Vault |
| [Data](https://granit-fx.dev/dotnet/data/) | Persistence, caching, blob storage, multi-tenancy |
| [API](https://granit-fx.dev/dotnet/api/) | Endpoints, OpenAPI, versioning, idempotency |
| [Infrastructure](https://granit-fx.dev/dotnet/infrastructure/) | Messaging, background jobs, notifications, webhooks |
| [Business](https://granit-fx.dev/dotnet/business/) | Workflow, data exchange, templating, document generation |
| [AI](https://granit-fx.dev/dotnet/ai/) | AI abstractions, MCP, vector data, extraction |
| [Compliance](https://granit-fx.dev/dotnet/compliance/) | GDPR, ISO 27001, audit trail |
| [Guides](https://granit-fx.dev/dotnet/guides/) | Step-by-step tutorials and recipes |
| [Contributing](https://granit-fx.dev/contributing/) | Conventions and contribution workflow |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and the [contribution guide](https://granit-fx.dev/contributing/) for conventions and workflow.

## Changelog

Changes are documented in [CHANGELOG.md](CHANGELOG.md).

## License

Licensed under the [Apache License 2.0](LICENSE).
