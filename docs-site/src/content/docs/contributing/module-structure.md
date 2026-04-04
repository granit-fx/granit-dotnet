---
title: "Module Structure \u2014 Anatomy of a Granit Package"
description: Anatomy of a Granit package — standard project layout, abstractions/endpoints/persistence layers, isolated DbContext checklist, and NuGet packaging conventions.
sidebar:
  label: Module Structure
  order: 3
---

Every Granit package follows a predictable directory structure. A developer
opening `Granit.Identity` should find the same layout as in `Granit.Workflow`
or `Granit.DataExchange`.

:::note[Good to know]
**Predictability > Freedom.** A new file must be classifiable without hesitation.
If the directory does not exist yet, create it following this blueprint.
:::

## Business package blueprint

```text
Granit.Example/
├── Domain/                    Entities, aggregates, Value Objects, domain enums
├── Events/                    Wolverine messages (events, commands)
├── Exceptions/                Business exceptions (inherit BusinessException, etc.)
├── Extensions/                Extension methods (DI, builders, etc.)
├── Handlers/                  Wolverine handlers (event/command consumers)
├── Internal/                  Internal implementations (services, stores, etc.)
├── Localization/              JSON translation files (18 cultures)
├── Options/                   IOptions<T> configuration classes
├── GranitExampleModule.cs     Module class (required at root)
├── IExampleReader.cs          Public interfaces (at module root)
├── IExampleWriter.cs
├── Granit.Example.csproj
└── README.md
```

## Endpoints package blueprint

```text
Granit.Example.Endpoints/
├── Dtos/                      Request/Response records (API contracts)
├── Endpoints/                 Minimal API classes (MapGet, MapPost, etc.)
├── Extensions/                EndpointRouteBuilderExtensions
├── Internal/                  Internal implementations
├── Localization/              JSON translation files
├── Options/                   Endpoint configuration
├── Permissions/               PermissionDefinitionProvider
├── Validators/                FluentValidation validators
├── GranitExampleEndpointsModule.cs
├── Granit.Example.Endpoints.csproj
└── README.md
```

## EntityFrameworkCore package blueprint

```text
Granit.Example.EntityFrameworkCore/
├── Configurations/            Fluent API (IEntityTypeConfiguration<T>)
├── Entities/                  EF-only entities (not in the business package)
├── Extensions/                ServiceCollectionExtensions (AddDbContextFactory, etc.)
├── Internal/                  DbContext, EfCoreStore implementations
├── GranitExampleEntityFrameworkCoreModule.cs
├── IExampleDbContext.cs       Host interface (optional, for shared DbContexts)
├── Granit.Example.EntityFrameworkCore.csproj
└── README.md
```

## Standard directory reference

### Universal directories (all packages)

| Directory | Content | Expected visibility |
| --------- | ------- | ------------------- |
| `Extensions/` | Extension methods (`IServiceCollection`, `IApplicationBuilder`, `ModelBuilder`) | `public static` |
| `Internal/` | Concrete implementations, services, stores, handlers, DbContext | `internal sealed` |
| `Options/` | Configuration classes bound to `IOptions<T>` / `IOptionsMonitor<T>` | `public` |
| `Localization/` | JSON translation files (`en.json`, `fr.json`, etc.) | -- |

### Domain directories (business packages)

| Directory | Content | Expected visibility |
| --------- | ------- | ------------------- |
| `Domain/` | Rich entities, aggregates, Value Objects, domain enums | `public` |
| `Events/` | Wolverine messages (integration events, commands) | `public sealed record` |
| `Exceptions/` | Business exceptions (`BusinessException`, `NotFoundException`, etc.) | `public sealed` |
| `Handlers/` | Wolverine handlers for events/commands | `internal sealed` |

### API directories (*.Endpoints packages)

| Directory | Content | Expected visibility |
| --------- | ------- | ------------------- |
| `Dtos/` | Request (`*Request`) and response (`*Response`) records | `public sealed record` |
| `Endpoints/` | Minimal API classes declaring routes | `internal sealed` |
| `Permissions/` | `*PermissionDefinitionProvider` | `internal sealed` |
| `Validators/` | FluentValidation validators (`*Validator`) | `internal sealed` |

### Persistence directories (*.EntityFrameworkCore packages)

| Directory | Content | Expected visibility |
| --------- | ------- | ------------------- |
| `Configurations/` | `IEntityTypeConfiguration<T>` (Fluent API) | `internal sealed` |
| `Entities/` | EF-only entities not present in the business package | `public sealed` |

## Forbidden directories

| Name | Why forbidden | Use instead |
| ---- | ------------- | ----------- |
| `Helpers/`, `Utils/`, `Common/` | No semantics -- becomes a dumping ground | `Internal/` or a thematic directory |
| `Services/` | Too generic -- says nothing about responsibility | `Internal/` for implementations, root for interfaces |
| `Abstractions/` | Granit does not separate interfaces into a dedicated package | Module root |
| `Models/` | Ambiguous between DTO, entity, and ViewModel | `Domain/` for entities, `Dtos/` for DTOs |
| `DbContext/` (directory) | Legacy pattern | `Internal/` |
| `Stores/` (directory) | Stores are internal implementations | `Internal/` |

## Placement rules

| File type | Location | Example |
| --------- | -------- | ------- |
| Public interface | **Module root** | `IBlobStorage.cs` |
| Public record (service contract) | **Module root** | `BlobUploadRequest.cs` |
| Module class | **Module root** | `GranitBlobStorageModule.cs` |
| Domain entity | **`Domain/`** | `Domain/ExportJob.cs` |
| Business exception | **`Exceptions/`** | `Exceptions/BlobNotFoundException.cs` |
| Wolverine event | **`Events/`** | `Events/BlobDeletedEvent.cs` |
| Internal implementation | **`Internal/`** | `Internal/DefaultBlobStorage.cs` |
| Options class | **`Options/`** | `Options/BlobStorageOptions.cs` |
| DI extension | **`Extensions/`** | `Extensions/BlobStorageServiceCollectionExtensions.cs` |

:::caution[Warning]
Any `internal` type must reside in `Internal/` or in a thematic subdirectory
(`Validators/`, `Endpoints/`, `Configurations/`, etc.). An `internal` type at the
module root will fail architecture tests.
:::

### Maximum depth

**2 levels maximum** under the module. The IDE handles search -- no need for
deep nesting.

```text
OK    Granit.Foo/Internal/MyStore.cs
OK    Granit.Foo/Domain/MyEntity.cs
BAD   Granit.Foo/Domain/SubDomain/Values/MyValue.cs   (too deep)
```

Exception: complex multi-feature modules (e.g., `Granit.DataExchange`) may use
feature-level directories at the first level, then standard directories inside.

## Namespace synchronization

The namespace must **always** match the directory structure:

```text
src/Granit.Foo/Internal/MyStore.cs  ->  namespace Granit.Foo.Internal;
src/Granit.Foo/Domain/MyEntity.cs   ->  namespace Granit.Foo.Domain;
src/Granit.Foo/IMyService.cs        ->  namespace Granit.Foo;
```

This is enforced by the architecture test
`Namespace_should_match_folder_structure_in_src`.

## Isolated DbContext checklist

Every `*.EntityFrameworkCore` package that owns an isolated `DbContext` **must**
follow this checklist with no exceptions:

1. **`<ProjectReference>` to `Granit.Persistence`** in the `.csproj`
2. **Constructor injection** of `ICurrentTenant?` and `IDataFilter?` (both
   optional, default `null`)
3. **Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)`** at
   the end of `OnModelCreating`
4. **Interceptor wiring** in the extension method: use the `(sp, options)`
   overload of `AddDbContextFactory` with `ServiceLifetime.Scoped` and resolve
   `AuditedEntityInterceptor` / `SoftDeleteInterceptor` from the service provider
5. **`[DependsOn(typeof(GranitPersistenceModule))]`** on the module class
6. **No manual `HasQueryFilter`** in entity configurations --
   `ApplyGranitConventions` handles all standard filters centrally
7. **`IMultiTenant` entities** use `Guid? TenantId` (never `string`)

:::tip[Pro tip]
These rules are enforced automatically by `Granit.ArchitectureTests`
(`IsolatedDbContextTests`). Run `dotnet test tests/Granit.ArchitectureTests`
to verify compliance before pushing.
:::

## Validator registration

Modules decorated with `[assembly: WolverineHandlerModule]` get automatic
validator discovery via `AddGranitWolverine()`. Modules **without** Wolverine
handlers must call `AddGranitValidatorsFromAssemblyContaining<TValidator>()`
manually. Without registration, `FluentValidationEndpointFilter<T>` silently
skips validation.

## New module checklist

1. Create the project: `dotnet new classlib -n Granit.Example`
2. Add `GranitExampleModule.cs` at the root
3. Create `Extensions/ExampleServiceCollectionExtensions.cs` with
   `AddGranitExample()`
4. Create `README.md`
5. Create the mirror test project: `tests/Granit.Example.Tests/`
6. Add directories as needed from the blueprints above
7. Verify with `dotnet test tests/Granit.ArchitectureTests` that the structure
   is compliant
