---
title: "FAQ \u2014 Frequently Asked Questions"
description: Answers to the most common questions about Granit — Wolverine optionality, EF Core vs repository pattern, multi-tenancy without GDPR enforcement, and NuGet publishing.
sidebar:
  label: FAQ
  order: 3
---

## Do I need Wolverine to use Granit?

No. Wolverine is an optional dependency. Granit's module system, persistence,
caching, authentication, and most other features work without Wolverine.

When Wolverine is not installed, Granit provides channel fallbacks for features
that would otherwise use messaging (e.g., notifications, background jobs). You
lose transactional outbox and durable message handling, but the core
functionality remains available.

See [Wolverine Optionality](/dotnet/concepts/wolverine-optionality/) for the full
details on what changes with and without Wolverine.

## Which database is supported?

**PostgreSQL** is the primary and fully tested database. All Granit
`*.EntityFrameworkCore` packages are tested against PostgreSQL.

**SQL Server** and **SQLite** are possible through EF Core's provider
abstraction, but they are not part of the automated test suite. If you use them:

- Sequential GUID generation (`Granit.Guids`) is optimized for PostgreSQL
  clustered indexes. SQL Server uses a different byte-ordering for `NEWSEQUENTIALID()`.
- Some migration scripts may use PostgreSQL-specific SQL (e.g., `jsonb` columns).
- The Wolverine transactional outbox (`Granit.Wolverine.Postgresql`) is
  PostgreSQL-only by design.

## Can I use Granit for microservices?

Yes. Granit is designed for both modular monolith and microservice architectures.
Each Granit module is a self-contained unit with its own isolated `DbContext`,
service registrations, and API endpoints.

To extract a module into a standalone service:

1. Create a new ASP.NET Core host project
2. Reference only the Granit modules that service needs
3. Configure the module's `DbContext` to point at a separate database
4. Use Wolverine messaging (or HTTP) for inter-service communication

See [Modular Monolith vs Microservices](/dotnet/concepts/modular-monolith-vs-microservices/)
for architecture guidance.

## What is the minimum .NET version?

**NET 10** is the minimum supported version. Granit uses C# 14 language features
and .NET 10 APIs (`TimeProvider`, `[GeneratedRegex]` improvements, named query filters,
etc.) that are not available in earlier runtimes.

There are no plans to backport to .NET 8 or .NET 9.

## How do I add a custom module?

A Granit module is a class library with a single `GranitModule` subclass. The
minimum structure:

```text
src/MyCompany.MyModule/
  MyCompanyMyModuleModule.cs        <-- GranitModule subclass
  MyCompanyMyModuleModule.csproj
  Extensions/
    ServiceCollectionExtensions.cs  <-- AddMyModule() method
```

The module class declares dependencies and registers services:

```csharp
[DependsOn(typeof(GranitCoreModule))]
public sealed class MyCompanyMyModuleModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMyModule();
    }
}
```

See [Module Structure](/contributing/module-structure/) for the full guide,
including EF Core integration, endpoint mapping, and testing conventions.

## How does multi-tenancy work?

`ICurrentTenant` is available in every module through `Granit.MultiTenancy`
without referencing `Granit.MultiTenancy`. A `NullTenantContext` (`IsAvailable =
false`) is registered by default.

When `Granit.MultiTenancy` is installed, it provides the actual tenant resolution
(from headers, claims, route values, etc.) and populates the tenant context.

Entities implement `IMultiTenant` with a `Guid? TenantId` property.
`ApplyGranitConventions` adds a global query filter that automatically scopes
queries to the current tenant.

## How does localization work?

Granit supports **17 cultures**: 14 base languages (en, fr, nl, de, es, it, pt,
zh, ja, pl, tr, ko, sv, cs) plus 3 regional variants (fr-CA, en-GB, pt-BR).

Every module that uses localization must provide JSON resource files for all 17
cultures. Regional variant files only contain keys that differ from the base
language (`en.json` covers both `en-US` and `en` -- no `en-US.json` is needed).

`Granit.Localization.SourceGenerator` generates strongly-typed key accessors from
the JSON files at compile time.

## How do I run only the tests for a specific package?

Use the `--filter` option with the test project name:

```bash
dotnet test --filter "FullyQualifiedName~Granit.Caching.Tests"
```

Or run the specific test project directly:

```bash
dotnet test tests/Granit.Caching.Tests/Granit.Caching.Tests.csproj
```

## What compliance standards does Granit support?

Granit is designed with **GDPR** and **ISO 27001** compliance
built into its architecture:

- **GDPR**: Data minimization, right to erasure (`ISoftDeletable`,
  `IProcessingRestrictable`), pseudonymization via `Granit.Privacy`, encryption
  at rest via `Granit.Vault`
- **ISO 27001**: Audit trails (`AuditedEntityInterceptor`, `Granit.Timeline`),
  encryption in transit and at rest, secret management via HashiCorp Vault

See [Compliance](/dotnet/concepts/compliance/) for detailed mapping of Granit features
to compliance requirements.
