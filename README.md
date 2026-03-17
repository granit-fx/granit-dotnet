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
</p>

---

Granit is a rock-solid, production-ready modular framework for .NET and React.
Built as a Modular Monolith with zero compromises on Developer Experience.
It provides **100 NuGet packages** organized as independent modules,
compliant with **GDPR/ISO 27001** requirements.

## Features

| Domain | What Granit provides |
| --- | --- |
| **Core & Modularity** | Self-configuring module system, topological dependency sorting, timing, GUID generation |
| **Security** | JWT Bearer authentication, RBAC, Vault Transit encryption, dynamic credentials |
| **Identity** | Identity provider abstractions, user cache (cache-aside, login-time sync, GDPR) |
| **Persistence** | EF Core interceptors: audit trail (3 years), GDPR soft delete, multi-tenancy, settings, features |
| **Multi-tenancy** | Schema or database isolation, automatic resolution, transparent filtering |
| **Caching** | Distributed caching (HybridCache, Redis), AES-256 value encryption |
| **Observability** | Structured logging + distributed tracing → OTLP, health checks, metrics |
| **Messaging** | Transactional outbox, HMAC-SHA256 webhooks, notifications (6 channels), cron jobs |
| **API** | Versioning, OpenAPI Scalar, Stripe-style idempotency, ProblemDetails, CORS |
| **Storage & Imaging** | S3-compatible blob storage, pre-signed URLs, Crypto-Shredding, image processing |
| **Documents** | Template engine (Scriban), HTML→PDF rendering, Excel generation |
| **Data Exchange** | Import (Extract→Map→Validate→Execute), Export (tabular Excel/CSV with presets) |
| **Workflow** | FSM engine, publication lifecycle, approval routing |
| **Localization** | i18n (17 cultures), override store, source-generated keys |
| **SaaS** | Feature flags per commercial plan, quotas, Default → Plan → Tenant resolution |
| **Quality** | Embedded Roslyn analyzers, Architecture Tests (ArchUnitNET), FluentValidation (VAT, SIREN, NISS) |

## Quick start

```bash
# Add the foundation package to your project
dotnet add package Granit.Core

# Add the modules you need
dotnet add package Granit.Persistence
dotnet add package Granit.Security
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
    typeof(GranitSecurityModule),
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
| [Getting Started](docs/guide/getting-started.md) | Build a working Granit API in under 5 minutes |
| [Framework](docs/framework/index.md) | Architecture, modules, security, data, API, messaging, storage |
| [Tests](docs/testing/index.md) | xUnit conventions, mocking, assertions, EF Core integration |
| [Package catalogue](docs/index.md) | Complete list of 100 packages with their roles |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for conventions and contribution workflow.

## Changelog

Changes are documented in [CHANGELOG.md](CHANGELOG.md).

## License

Licensed under the [Apache License 2.0](LICENSE).
