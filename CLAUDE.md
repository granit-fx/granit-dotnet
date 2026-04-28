# CLAUDE.md - Granit

## Project

- **Type**: Modular .NET framework — 128 packages, 134 test projects
- **Repo**: `granit-dotnet` (company-level, not product-specific)
- **License**: Apache-2.0 (open-source)
- **Compliance**: GDPR + ISO 27001
- **Publication**: nuget.org (planned), GitHub Packages (internal)

## Stack & versions

.NET 10 (LTS) | C# 14 | EF Core 10 | VaultSharp 1.17+ | Serilog 9+ | OpenTelemetry 1.15+

## Architecture

```text
src/
  Granit/                             # Module system, shared domain types
  Granit.{Module}/                         # Abstractions + DI registration (e.g. Granit.BlobStorage)
  Granit.{Module}.Endpoints/               # Minimal API endpoints
  Granit.{Module}.EntityFrameworkCore/      # Isolated DbContext, EF configurations, migrations
  Granit.{Module}.{Provider}/              # Provider implementations (e.g. .S3, .AzureBlob, .Keycloak)
  Granit.{Module}.Wolverine/               # Wolverine message handlers
  Granit.{Module}.Notifications/           # Notification channel integration
  bundles/
    Granit.Bundle.{Name}/                  # Meta-packages grouping related modules

tests/
  Granit.{Module}.Tests/                   # Unit tests (xUnit + Shouldly + NSubstitute + Bogus)
  Granit.{Module}.Tests.Integration/       # Integration tests (Testcontainers)
  Granit.ArchitectureTests/                # Cross-cutting architecture rules (NetArchTest)

templates/
  granit-api/                              # dotnet new template: minimal API project
  granit-api-full/                         # dotnet new template: full API project
  granit-module/                           # dotnet new template: new Granit module

docs-site/                                 # Astro + Starlight documentation site
```

### Package naming convention

One project = one NuGet package. Namespace = project name. Zero circular refs.

Discover packages: `ls src/` — do NOT rely on a hardcoded list.

### Module anatomy

Each module follows a consistent layered split:

| Layer | Project suffix | Contains |
| ----- | -------------- | -------- |
| Abstractions | `Granit.{Module}` | Interfaces, options, DI extension, `*Module` class, **declarative definitions** (`*QueryDefinition`, `*ExportDefinition`) |
| Background Jobs | `.BackgroundJobs` | `IBackgroundJob` records, handlers, module class |
| Endpoints | `.Endpoints` | Minimal API route groups, request/response DTOs, validators, permission providers |
| Persistence | `.EntityFrameworkCore` | Isolated `DbContext`, entity configs, migrations |
| Provider | `.{Provider}` | External service implementation (S3, Keycloak, SMTP...) |
| Messaging | `.Wolverine` | Wolverine handlers, saga state machines |

## Commands

> ⚠️ **NEVER `dotnet build` / `dotnet test` on the full solution.** The repo (128 packages,
> 134 test projects) is too large — Roslyn OOMs and `dotnet format` chokes. **Always
> target a `.slnf` shard filter or a single project.**

```bash
# Shard build/test (PREFERRED — these are the CI shards)
dotnet build .github/shard-filters/core-ai.slnf
dotnet build .github/shard-filters/business.slnf
dotnet build .github/shard-filters/api-data.slnf
dotnet build .github/shard-filters/infrastructure.slnf
dotnet build .github/shard-filters/security.slnf
dotnet build .github/shard-filters/saas.slnf
dotnet build .github/shard-filters/integration.slnf
dotnet build .github/shard-filters/architecture.slnf
dotnet test  .github/shard-filters/<shard>.slnf --no-build

# Single package (PREFERRED for tight feedback loops)
dotnet build src/Granit.BlobStorage
dotnet test tests/Granit.BlobStorage.Tests --no-build

# Architecture tests
dotnet test tests/Granit.ArchitectureTests

# Format — also shard-scoped (full solution is too slow)
dotnet format .github/shard-filters/<shard>.slnf --verify-no-changes

# Pack for local feed
dotnet pack -c Release -o ./nupkgs

# Docs site
cd docs-site && npx astro build   # must produce 0 errors
```

**Picking the right shard:** match the directory you're touching against the
[Shard mapping](#ci-test-sharding--mandatory-when-adding-test-projects) table below.
When in doubt, the file you're editing belongs to the shard listed in
[`.github/test-shards.json`](.github/test-shards.json).

## Code quality

Modern C# 14 with idiomatic .NET 10. Write code that a senior .NET developer would recognize
as current and clean. Actively use the latest language and runtime features listed below.

### C# 14 features — use by default

- **Primary constructors**: for all DI service classes. Keep `private readonly T _x = x;`
  fields when the parameter is used in multiple methods.
- **Collection expressions**: `[x, y]` instead of `new[] { x, y }`, `[]` instead of
  `Array.Empty<T>()` or `new List<T>()`. Exception: `new[]` inside anonymous types
  (JSON payloads) where the compiler cannot infer the target type.
- **`field` keyword** (semi-auto properties): use `set => field = value;` in properties
  with custom setter logic instead of declaring a manual backing field.
- **Extension members** (`extension` blocks): prefer over static extension classes when
  adding multiple related members to the same type (especially FluentValidation
  `IRuleBuilder<T,P>` extensions).
- **`nameof` on unbound generics**: `nameof(List<>)` returns `"List"` — use for concrete
  generic types. NEVER use `nameof(T)` on a type parameter (returns the param name
  `"T"`, not the runtime type name — use `typeof(T).Name` instead).
- **Pattern matching**: prefer `is`, `switch` expressions, list patterns, property patterns.
- **File-scoped namespaces**: always (one `namespace X;` per file, no braces).

### C# 13 features — use by default

- **`System.Threading.Lock`**: ALWAYS use `private readonly Lock _lock = new();` for
  synchronization. NEVER lock on `object`, collections, or `this`.
- **`params ReadOnlySpan<T>`**: prefer over `params T[]` for non-attribute methods to
  reduce allocations. Attributes must keep `params T[]` (compile-time constraint).
- **`\e` escape**: use `\e` instead of `\u001b` or `\x1b` for ESCAPE characters.

### .NET 10 / EF Core 10 features — use by default

- **Named Query Filters** (EF Core 10): use `HasQueryFilter(name, expr)` for individually
  toggleable filters. See `GranitFilterNames` constants. Never use unnamed
  `HasQueryFilter(expr)`.
- **Native OpenAPI 3.1**: use `Microsoft.AspNetCore.OpenApi` + `AddOpenApi()` /
  `MapOpenApi()`. NEVER use Swashbuckle or NSwag. Scalar UI for documentation.
- **`IMeterFactory`**: use for metrics creation. NEVER use `new Meter(...)` directly.
- **`ActivitySource`**: native .NET diagnostics, one per module
  (`{Module}ActivitySource.cs`). Register via `GranitActivitySourceRegistry`.
- **`string.Split(char, count)`**: prefer `"a:b".Split(':', 2)` over `Split(new[]{':'}, 2)`
  (except in `netstandard2.0` source generators).

## Code conventions

Full standards: [`docs/guide/conventions/`](docs/guide/conventions/index.md)

### Must-use patterns

- **`var`**: when type is apparent; explicit type otherwise (IDE0008)
- **Expression body** (`=>`): for single-statement methods (IDE0022)
- **String interpolation**: prefer `$"..."` over `string.Concat(...)` or `string.Format(...)`
- **`[GeneratedRegex]`**: ALWAYS — never `new Regex(..., Compiled)`. Timeout on user input.
- **`[LoggerMessage]`**: ALWAYS — never string interpolation in log calls
- **`TimeProvider` / `IClock`**: NEVER `DateTime.Now`/`UtcNow`
- **`ConfigureAwait(false)`**: in library code. `CancellationToken` as last param.
- **`ArgumentNullException.ThrowIfNull()`**: over manual null checks
- **`ArgumentException.ThrowIfNullOrEmpty()`** / `ThrowIfNullOrWhiteSpace()`: for strings
- **`AddAuthorizationBuilder()`**: not `AddAuthorization(Action<>)` (ASP0025)

### Metrics & diagnostics — MANDATORY conventions

- **Directory**: `Diagnostics/` folder in the base module project
- **Class**: `sealed class {Module}Metrics` with `IMeterFactory` constructor injection
- **Meter name**: `"Granit.{Module}"` (PascalCase, one per module)
- **Metric name**: `granit.{module}.{entity}.{action}` (all lowercase, dot-separated)
- **Tags**: `snake_case`, always include `tenant_id` (coalesced to `"global"`), passed via `TagList`
- **DI**: `services.TryAddSingleton<{Module}Metrics>();`
- **ActivitySource**: `internal static class {Module}ActivitySource` in same `Diagnostics/` folder
- **Registration**: `GranitActivitySourceRegistry.Register(Name)` in `Add*()` extension

### Permissions — naming convention (STRICT)

All permission strings MUST follow the `[Group].[Resource].[Action]` format (three dot-separated
segments). Enforced across all `*.Endpoints` modules.

| Component | Convention | Example |
| --------- | ---------- | ------- |
| **Group** | `{Module}Permissions.GroupName` (PascalCase) | `"BackgroundJobs"`, `"BlobStorage"` |
| **Resource** | Nested static class name (plural noun) | `Jobs`, `Blobs`, `Templates`, `Flags` |
| **Action** | Verb describing the access level | `Read`, `Manage`, `Execute`, `Create` |

- **Permission constant**: `public const string Read = "{Group}.{Resource}.Read";`
- **Localization key (group)**: `PermissionGroup:{Group}` (e.g. `"PermissionGroup:BackgroundJobs"`)
- **Localization key (permission)**: `Permission:{Group}.{Resource}.{Action}` (e.g. `"Permission:BackgroundJobs.Jobs.Read"`)
- **Provider class**: `internal sealed class {Module}PermissionDefinitionProvider : IPermissionDefinitionProvider`
- **Localization resource**: `internal sealed class {Module}EndpointsLocalizationResource` with `[LocalizationResourceName]`
- **Auto-discovery**: providers are auto-discovered by `GranitAuthorizationModule` (no manual registration)
- **Standard actions**: `Read` (consultation, never `View`), `Manage` (grouped writes),
  `Execute` (single actions), `Create` (resource creation). Domain-specific actions
  (`Upload`, `Download`, `Delete`, `Revoke`, `Rotate`, `Sync`) are allowed when
  `Manage` would be too coarse for least-privilege (ISO 27001 A.9.4)

### Events — naming convention (STRICT)

Two suffixes — enforced by architecture tests:

| Suffix | Interface | Dispatch | Example |
| ------ | --------- | -------- | ------- |
| `*Event` | `IDomainEvent` or none | `AddDomainEvent()` (aggregate) or `ILocalEventBus.PublishAsync()` (service) | `BlobValidatedEvent` |
| `*Eto` | `IIntegrationEvent` | `AddDistributedEvent()` (aggregate) or `IDistributedEventBus.PublishAsync()` (service) → Wolverine outbox | `PersonalDataDeletedEto` |

- Past-tense verb + suffix. NEVER bare names (`BlobValidated` → `BlobValidatedEvent`)
- Generic lifecycle: `EntityCreatedEvent<T>` / `EntityCreatedEto<T>` via `IEmitEntityLifecycleEvents`

### Wolverine handler visibility — CRITICAL

Wolverine discovers handlers via `Assembly.ExportedTypes` and requires **public types
with public constructors** ([docs](https://wolverinefx.net/guide/handlers/)).
`static class` types are additionally skipped unless decorated with
`[WolverineHandler]`, which creates an unwanted dependency on WolverineFx.

- **Handlers MUST be `public class` (non-static) with `public static` Handle methods.**
  This ensures discovery without any attribute or WolverineFx dependency.
- NEVER use `public static class` — requires `[WolverineHandler]` to be discovered.
- NEVER use `internal` — invisible to `Assembly.ExportedTypes`.
- NEVER add a `protected` constructor — Wolverine requires a public constructor for
  discovery. The implicit parameterless public constructor is intentional.
- If a handler injects an `internal` service, make the service `public` or extract an
  interface. Never make the handler `internal` to match the service visibility.
- Background job handlers follow the same rule.
- **SonarQube S1118** ("utility classes should not have public constructors") is a
  **false positive** on these handlers — mark as **Won't Fix**. These are not utility
  classes; they are convention-discovered message handlers.

### Background Jobs — naming convention (STRICT)

Single category with **mandatory suffix** — enforced by architecture tests:

| Interface | Attribute | Suffix | Example | Location |
| --------- | --------- | ------ | ------- | -------- |
| `IBackgroundJob` | `[RecurringJob]` | `*Job` | `OrphanBlobCleanupJob` | `Granit.{Module}.BackgroundJobs/Jobs/` |

- **`*Job`**: `sealed record` implementing `IBackgroundJob`, decorated with
  `[RecurringJob("cron", "name")]`. Handler in same `Jobs/` folder.
- **Dedicated sub-project**: each module's jobs live in `Granit.{Module}.BackgroundJobs`
  with a `Granit{Module}BackgroundJobsModule : GranitModule` class. This keeps the base
  module free from the `Granit.BackgroundJobs` dependency.
- **Job name format**: `{module-kebab}-{action-kebab}` (e.g., `"blob-storage-orphan-cleanup"`).
  Module prefix ensures global uniqueness.
- **Handler naming**: `{Action}Handler` (e.g., `OrphanBlobCleanupHandler`) — `public static partial class`.
- **NEVER** use `*Command` suffix for jobs — commands are CQRS, jobs are scheduled work units.
- **NEVER** create a separate `.Wolverine` package for jobs. Wolverine scheduling is
  handled by `Granit.BackgroundJobs.Wolverine`.

### `*.Notifications` package — canonical checklist (STRICT)

Every `Granit.{Module}.Notifications` package follows the same shape. Reference
implementations: `Granit.Privacy.Notifications` (most recent, embedded templates +
`{{ privacy }}` global context + manifest pinning test) and
`Granit.Identity.Local.Notifications` (largest catalog: 9 notification types,
145 templates).

Public docs equivalent (audience: open-source consumers):
[`docs-site/src/content/docs/dotnet/infrastructure/notifications/conventions.mdx`](docs-site/src/content/docs/dotnet/infrastructure/notifications/conventions.mdx).
Keep the two in sync.

#### 1. csproj structure

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Granit.{Module}.Notifications</PackageId>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Granit.{Module}.Notifications.Tests" />
    <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="Templates\**\*.html" WithCulture="false" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Granit.{Module}\Granit.{Module}.csproj" />
    <ProjectReference Include="..\Granit.Notifications.Abstractions\Granit.Notifications.Abstractions.csproj" />
    <ProjectReference Include="..\Granit.Templating\Granit.Templating.csproj" />
  </ItemGroup>
</Project>
```

`WithCulture="false"` is **mandatory** — without it MSBuild treats files like
`privacy.export_ready.fr.html` as satellite resources and breaks the manifest layout.

#### 2. Module class — `Granit{Module}NotificationsModule.cs`

```csharp
[DependsOn(
    typeof(GranitNotificationsAbstractionsModule),
    typeof(Granit{Module}Module),
    typeof(GranitTemplatingModule))]
public sealed class Granit{Module}NotificationsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddEmbeddedTemplates(typeof(Granit{Module}NotificationsModule).Assembly);
        context.Services.AddTemplateLayout("{module-prefix}.*", "Layout.Email");
        // Optional — only if the module ships a global context (e.g. PrivacyContactGlobalContext).
        // context.Services.AddTemplateGlobalContext<{Module}ContactGlobalContext>();
        // Optional — only when the module needs to expose definitions to the admin UI.
        // context.Services.AddSingleton<INotificationDefinitionProvider, {Module}NotificationDefinitionProvider>();
    }
}
```

Layout glob: `"{module-prefix}.*"` — covers all snake_case names. Add a second
`"{Module}.*"` line if the package still ships legacy PascalCase template names
(see `Granit.Privacy.Notifications` → `Privacy.LegalDocumentObsolete`).

#### 3. Notification types — `*NotificationType.cs`

```csharp
public sealed class {Action}NotificationType : NotificationType<{Action}NotificationData>
{
    public static readonly {Action}NotificationType Instance = new();
    public override string Name => "{module}.{event_name}";          // snake_case — preferred
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

public sealed record {Action}NotificationData(/* fields */);
```

- **Naming**: `snake_case` is preferred (`privacy.deletion_acknowledged`,
  `customer-balance.credit_expiring`). Legacy PascalCase (`Privacy.LegalDocumentObsolete`)
  still works — never repeat that pattern in new types.
- **`Name`** is the resource key for templates (`Templates/{Name}.html`) AND the
  notification definition id surfaced in the admin UI.
- **Singleton `Instance`** — Wolverine handlers reference it directly when calling
  `INotificationPublisher.PublishAsync(...)`.
- Data record stays in the same file (matches the rest of the codebase).

#### 4. JSON-array gotcha — `*Display` companion field (MANDATORY when shipping `IReadOnlyList<T>`)

The email channel serializes the data record to JSON and flattens it into a Scriban
dictionary via `JsonElementToDictionary` (in
`src/Granit.Notifications.Email/Internal/EmailNotificationChannel.cs`). **Arrays
round-trip as their JSON string representation and are NOT iterable in Scriban.**

Workaround: ship a `*Display` (string CSV) companion field alongside any list:

```csharp
public sealed record PrivacyExportFailedNotificationData(
    Guid RequestId,
    string ArchiveBlobReferenceId,
    IReadOnlyList<string> MissingProviders,        // kept for audit/programmatic consumers
    string MissingProvidersDisplay,                // template-friendly: string.Join(", ", MissingProviders)
    DateTimeOffset RequestedAt,
    string Regulation);
```

Reference: `Granit.Privacy.Notifications/PrivacyExportFailedNotificationType.cs`.
Future US #1329 (architecture test D1) will warn when a list field has no `*Display` peer.

#### 5. Wolverine handlers — `Handlers/{Trigger}Handler.cs`

```csharp
public class {Trigger}NotificationHandler           // public class — NEVER static, NEVER internal
{
    public static async Task HandleAsync(           // public static method
        {Trigger}Eto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            {Action}NotificationType.Instance,
            new {Action}NotificationData(/* ... */),
            recipients: [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
```

**Wolverine routing — local vs distributed (decide per story).** Wolverine discovers the
handler the same way regardless of where the trigger comes from, but the *transport*
differs:

- **Local trigger** — emitted in the same process that hosts the `*.Notifications`
  package (e.g. `WebhookDeliveryFailureThresholdExceededEto` originating in
  `Granit.Webhooks` when both modules ship in the same API). Subscribe via the local
  event bus (`ILocalEventBus`) — no network hop, no outbox. Use a domain `*Event`
  rather than an `*Eto` if both producer and consumer live together.
- **Distributed trigger** — emitted by another microservice and consumed via the bus
  (e.g. `IdentityTokenExchangedEto` flowing from an identity service to a notifications
  worker). Configure the queue/topic routing in Wolverine and keep `*Eto` semantics.

Each Feature B / Feature C story under epic #1306 must explicitly state which mode it
uses — defaulting silently to distributed for an in-process event wastes a round trip
and breaks ordering guarantees.

See [Wolverine handler visibility — CRITICAL](#wolverine-handler-visibility--critical)
above. SonarQube S1118 on these handlers is a false positive — mark **Won't Fix**.

#### 6. Templates — `Templates/{Name}.html` (+ per-culture variants)

```html
<title>Your data has been deleted</title>
<p>Hi,</p>
<p>We confirm that your personal data has been permanently deleted on
   <strong>{{ model.executed_at | date.to_string '%B %d, %Y at %H:%M UTC' }}</strong>.</p>
<p>Reference: <code>{{ model.request_id }}</code></p>
{{ if privacy.dpo_email }}
<p>Contact our DPO at <a href="mailto:{{ privacy.dpo_email }}">{{ privacy.dpo_email }}</a>.</p>
{{ end }}
```

- **First line MUST be `<title>...</title>`** — the email channel extracts the subject
  from this tag. Without it, the subject defaults to `notification_type.Replace('.', ' ')`
  (ugly, e.g. `"privacy export_failed"`).
- **Neutral file = English** — `Templates/{Name}.html` (no culture suffix). Per-culture
  files: `Templates/{Name}.{culture}.html` (e.g. `.fr.html`, `.de.html`). Framework
  baseline ships **EN + FR**; the rest are generated by `scripts/translate-templates.py`
  (US #1311 / Feature A0) and carry a
  `<!-- AUTO-TRANSLATED ({model}, {date}) — REVIEW BEFORE PRODUCTION -->` marker.
- **Graceful degradation** — wrap optional fields in `{{ if ... }} ... {{ end }}` so
  hosts that don't populate the global context (e.g. `{{ privacy.dpo_email }}`) still
  render a usable email instead of `<empty href>` links.

#### 7. Template variables available

| Variable | Source | Notes |
| -------- | ------ | ----- |
| `{{ model.* }}` | The notification data record | Field names rendered `snake_case` via `StandardMemberRenamer` (`MissingProvidersDisplay` → `missing_providers_display`) |
| `{{ privacy.* }}` | `PrivacyContactGlobalContext` (when `Granit.Privacy.Notifications` is loaded) | Controller name/email, DPO email, supervisory authority URL |
| `{{ now.* }}` | Time provider | `now.utc_now`, `now.local_now` |
| `{{ context.* }}` | Notification dispatch context | Recipient locale, channel name, tenant id |
| `{{ app.* }}` | Host-provided global context | Branding (`app.name`, `app.support_email`, `app.logo_url`) — registered by the application, not the framework |

Each module that ships its own global context registers it via
`AddTemplateGlobalContext<TContext>()` in its `Granit{Module}NotificationsModule`.

#### 8. Tests — `tests/Granit.{Module}.Notifications.Tests/`

```text
tests/Granit.{Module}.Notifications.Tests/
  Handlers/
    {Trigger}NotificationHandlerTests.cs   # one per Wolverine handler
  Templates/
    EmbeddedTemplatesTests.cs              # manifest pinning theory test
```

Manifest pinning theory test (skeleton — copy from
`tests/Granit.Privacy.Notifications.Tests/Templates/EmbeddedTemplatesTests.cs`):

```csharp
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        "Templates.{module}.{name1}.html",        // EN neutral
        "Templates.{module}.{name1}.fr.html",     // FR baseline
        // ... one row per (notification, culture) shipped
    ];

    [Theory, MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(Granit{Module}NotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";
        assembly.GetManifestResourceNames().ShouldContain(fullResourceName);
    }
}
```

#### 9. CI test sharding (MANDATORY)

Add `tests/Granit.{Module}.Notifications.Tests` to the **`infrastructure`** shard of
[`.github/test-shards.json`](.github/test-shards.json) (where the rest of
`*.Notifications.*` lives). Then run `python3 scripts/generate-shard-filters.py` to
regenerate the `.slnf` files. The pre-push hook does this automatically when
`.csproj` or `test-shards.json` changes.

Without registration, CI silently skips the project.

#### 10. Translation strategy

- Framework ships **EN + FR** out of the box for every `*.Notifications` package.
- Additional cultures via `scripts/translate-templates.py` (US #1311 / Feature A0):
  reproducible, idempotent, validates Scriban placeholder integrity post-translation.
- Generated files carry `<!-- AUTO-TRANSLATED ({model}, {date}) — REVIEW BEFORE PRODUCTION -->`
  on the first line.
- Apps requiring legally-validated wording override embedded templates at runtime via
  the `Granit.Templating` admin API (the DB-backed resolver runs at higher priority
  than the embedded one).

#### 11. Architecture enforcement (future)

US #1329 (Feature D1) will assert, for every assembly matching `Granit.*.Notifications`:
for every type that derives from `NotificationType<>` and whose `DefaultChannels`
include a text-rendered channel (Email, SMS, WhatsApp, Web Push…), an `EmbeddedResource`
named `Templates.{Name}.html` is present in the same assembly. Cross-link this section
when the test lands.

### Declarative definitions (`*QueryDefinition`, `*ExportDefinition`) — placement (STRICT)

Two declarative primitives describe how an entity is consulted (grid filter/sort) and
exported (CSV/XLSX whitelist). They are **pure declarations** — no HTTP, no DbContext —
so they live with the domain, not with the HTTP layer.

| Primitive | Base class location | Concrete instance location |
| --------- | ------------------- | -------------------------- |
| `QueryDefinition<T>` | `Granit.QueryEngine.Abstractions` | `Granit.{Module}/Queries/{Entity}QueryDefinition.cs` |
| `ExportDefinition<T>` | `Granit.DataExchange.Abstractions` | `Granit.{Module}/Exports/{Entity}ExportDefinition.cs` |

**Rules:**

- **Concrete `*QueryDefinition` / `*ExportDefinition` classes MUST live in the base module
  `Granit.{Module}`**, NOT in `Granit.{Module}.Endpoints`. The `.Endpoints` package is
  reserved for HTTP-layer artifacts (route groups, request/response DTOs, validators,
  permission providers).
- **Base modules reference `Granit.QueryEngine.Abstractions` / `Granit.DataExchange.Abstractions`**
  (lightweight contracts, no runtime). NEVER reference `Granit.QueryEngine` or
  `Granit.DataExchange` directly from a base module.
- **Registration in the module class** (`Granit{Module}Module.ConfigureServices`):
  `services.AddQueryDefinition<TEntity, TDefinition>()` and
  `services.AddExportDefinition<TEntity, TDefinition>()`. Each module owns its
  registrations — NEVER aggregate them into a central package.
- **Localization keys** (`Column:{Entity}.{Field}`, `ExportHeader:{Entity}.{Field}`) live
  in the base module's localization resource alongside the definitions.
- **Naming**: `{Entity}QueryDefinition` and `{Entity}ExportDefinition` (no other suffix).
  `Name` property uses `"Granit.{Module}.{Entity}{Query|Export}"` (e.g.,
  `"Granit.Invoicing.InvoiceQuery"`, `"Granit.Invoicing.InvoiceExport"`).
- **Why this placement**: queries and exports are part of the module's public contract —
  the same definition is consumed by HTTP endpoints (`MapGranitQueryEndpoint`),
  background jobs (export orchestrator), and tests. Coupling the declaration to
  `.Endpoints` would make it inaccessible to non-HTTP consumers.

**Anti-pattern — central aggregation package**: NEVER create a `Granit.{X}.Definitions`
package that depends on every module to register definitions. Each module owns its own.

**Pairing rule — STRICT**: every "admin-visible" entity MUST have **both** a
`QueryDefinition` AND a matching `ExportDefinition`. Query and Export are two facets of
the same admin-grid use case (browse + export). If you add one, you MUST add the other.
Architecture tests enforce this pairing for every entity registered with either primitive.

**What counts as "admin-visible"**: any aggregate root or entity exposed in an admin
panel — typically those with a CRUD endpoint group or those returned in a paginated grid.
Pure infrastructure entities (audit log details, internal cache rows like
`AIWorkspaceEntity`, `TenantFeatureOverride`) are exempt and use the reflection-based
fallback.

### DTOs & API responses

- **Prefixed names**: `WorkflowTransitionRequest`, not `TransitionRequest` — OpenAPI flattens namespaces
- **Suffixes**: `*Request` for input, `*Response` for output. NEVER `*Dto`.
- **Errors**: Always `TypedResults.Problem(detail, statusCode)` (RFC 7807). Return type: `ProblemHttpResult`.
- **No entity exposure**: EF entities must NOT be returned — create `*Response` records.

### OpenAPI endpoint metadata — MANDATORY (5 elements)

Every endpoint MUST declare all 5 metadata elements, chained in this order:

```csharp
group.MapGet("/{id:guid}", GetByIdAsync)
    .WithName("GetBlobDescriptor")                          // PascalCase operation ID
    .WithSummary("Returns a blob descriptor by ID.")        // Imperative, ~100 chars, period
    .WithDescription("Fetches the full metadata...")        // 2-4 sentences: what, context, errors
    .Produces<BlobDescriptorResponse>()                     // Success response type
    .ProducesProblem(StatusCodes.Status404NotFound);        // One per error path
```

**Produces mapping:** Match handler return type → `Ok<T>` = `.Produces<T>()`,
`Created<T>` = `.Produces<T>(201)`, `NotFound` = `.ProducesProblem(404)`,
`ValidationProblem` = `.ProducesValidationProblem()`, `FileStreamHttpResult` =
`.Produces(200, contentType: "application/octet-stream")`. Never omit `.Produces()`.

**Rules:** WithName = PascalCase VerbNoun, WithSummary = imperative ~100 chars with period,
WithDescription = 2-4 sentences, ProducesProblem = one per error status code.

### OpenAPI tags — naming convention (STRICT)

Every `*.Endpoints` module MUST attach a `.WithTags(...)` call on its root `RouteGroupBuilder`.
Without it, .NET's OpenAPI generator falls back to the handler's declaring class name
(e.g. `AccountLoginEndpoints`) — ugly and leaks internal structure.

**Format: `Title Case With Spaces`.**

- Prefer natural English: `Blob Storage`, `Background Jobs`, `Reference Data`, `Data Exchange`.
- NEVER glued PascalCase (`BlobStorage`, `MobilePush`, `CustomerBalance`) — that's a class name, not a UI label.
- NEVER kebab-case or snake_case (those belong in URLs and metric tags, not OpenAPI UI labels).

**Module sub-tags: `<Module> - <SubGroup>`.**

When a module exposes multiple distinct tag groups, prefix every sub-tag with the module
name followed by ` - ` (space-dash-space). This makes Scalar's left column group them
visually and alphabetically.

| Module | Tags |
| ------ | ---- |
| `Granit.AI.Endpoints` | `AI - Workspaces`, `AI - Inference`, `AI - Providers`, `AI - Usage` |
| `Granit.Identity.Endpoints` | `Identity - User Cache`, `Identity - Provider`, `Identity - Webhook` |
| `Granit.Notifications.Endpoints` | `Notifications`, `Notifications - Mobile Push` |
| `Granit.MultiTenancy.Endpoints` | `Platform - Tenants` |

Single-tag modules use the module's user-facing name directly (e.g. `Blob Storage`,
`Privacy`, `Webhooks`, `Workflow`). The tag name does NOT have to match the .NET
namespace — it's a human label.

**Expose the tag via options.** Every module MUST ship a `TagName` property (or
`{Role}TagName` for multi-tag modules) on its `*EndpointsOptions` with a sensible
default. Consumers can override per-app.

**Declarative tag list.** `Granit.Http.ApiDocumentation` auto-emits a sorted
`document.Tags` array (via `SortedTagsDocumentTransformer`), so Scalar renders tags
alphabetically without per-app configuration.

### Validation

- **Auto-validation**: use `endpoints.MapGranitGroup(prefix)` instead of `MapGroup()` — applies
  `FluentValidationAutoEndpointFilter` automatically to all endpoints in the group
- **Validator discovery**: `GranitValidationModule` auto-discovers all `IValidator<T>` from
  loaded module assemblies (no manual registration needed)
- **Opt-out**: `group.MapPost("/x", Handler).WithMetadata(new SkipAutoValidationAttribute())`
- **OpenAPI enrichment**: `FluentValidationSchemaTransformer` exposes validation constraints
  (maxLength, pattern, required, etc.) in the OpenAPI schema for frontend code generators
- **Architecture tests**: `ValidationConventionTests` ensures all `*Request` types have
  validators and all route groups use `MapGranitGroup()`
- **Localized messages — MANDATORY**: NEVER use hardcoded `.WithMessage("...")` strings in
  validators. Built-in validators (NotEmpty, MaximumLength, etc.) are automatically converted
  to error codes by `GranitErrorCodeLanguageManager`. For custom `.Must()` validators, use
  `.WithErrorCodeAndMessage("Granit:Validation:XxxCode")` and add the corresponding key
  to all 17 JSON files in `src/Granit.Validation/Localization/Validation/`. The frontend
  resolves error codes to localized strings via `GET /api/granit/localization`.

### Isolated DbContext — MANDATORY for `*.EntityFrameworkCore` packages

Every isolated `DbContext` MUST:

1. `<ProjectReference>` to `Granit.Persistence`
2. Constructor-inject `ICurrentTenant?` and `IDataFilter?` (both optional, default `null`)
3. Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at end of `OnModelCreating`
4. Wire interceptors via `(sp, options)` overload of `AddDbContextFactory` (Scoped), resolving `AuditedEntityInterceptor` / `SoftDeleteInterceptor`
5. `[DependsOn(typeof(GranitPersistenceModule))]` on module class
6. **No manual `HasQueryFilter`** — `ApplyGranitConventions` handles all standard filters
7. `IMultiTenant` entities use `Guid? TenantId` (never `string`)

Reference: [`docs/framework/data/persistence.md`](docs/framework/data/persistence.md)

### DDD — Aggregate Root vs Entity

Use `AggregateRoot` (or audited variants) when the entity has a state machine, raises
domain events, or encapsulates invariants. Use plain `Entity` for append-only records,
configuration, caches, or lookup tables.

**Aggregate Root rules (enforced by `DomainConventionTests`):**

- **Private setters**: all properties `{ get; private set; }`. Use behavior methods for
  state transitions (e.g., `MarkAsValid()`, `Revoke()`)
- **Factory method**: `public static Xxx Create(...)` — the only way to construct
- **Private EF Core constructor**: keep `private Xxx() { }` for materialization
- **Explicit interface for `IMultiTenant`**: when `TenantId` has `private set`, add
  explicit `Guid? IMultiTenant.TenantId { get; set; }` for interceptor injection
- **Domain events via base class**: use `AddDomainEvent()` / `AddDistributedEvent()` —
  NEVER manually implement `IDomainEventSource`
- **No public setters on aggregate roots** — architecture test enforces this

**Value Objects (`SingleValueObject<T>`):**

- Inherit from `SingleValueObject<T>` for single-primitive wrappers
- Must be `sealed` with `init` properties
- Provide `Create()` factory with validation + implicit operators for backward compat
- EF Core converters auto-applied by `ApplyGranitConventions` (no migration needed)
- JSON serialization handled by `SingleValueObjectJsonConverterFactory`

Reference: [ADR-017](docs-site/src/content/docs/dotnet/architecture/adr/017-ddd-aggregate-value-object-strategy.md)

### Multi-tenancy — soft dependency

`ICurrentTenant` lives in `Granit.MultiTenancy` — available everywhere without referencing `Granit.MultiTenancy`.

- Use `using Granit.MultiTenancy;` — do NOT add `[DependsOn(GranitMultiTenancyModule)]`
- Always check `IsAvailable` before using `Id` — `NullTenantContext` is the default
- Hard dependency on `Granit.MultiTenancy` only when enforcing strict tenant isolation (GDPR)

### `[DependsOn]` convention

- **Direct = declare it.** Every `<ProjectReference>` with a `*Module` needs a `[DependsOn]`
- **Transitive = omit it.** Already pulled in by another declared dependency → skip
- **`Granit`** = implicit base, never needs `DependsOn`
- **Alphabetical order** for `DependsOn` entries
- **Zero-dependency modules** have no `[DependsOn]` attribute — this is correct

### Tests

Each package has `*.Tests` project (xUnit + Shouldly + NSubstitute + Bogus). Part of DoD.

### CI test sharding — MANDATORY when adding test projects

Tests run in **8 parallel shards**: 6 unit-test shards aligned with the
architecture layers, plus a dedicated `integration` shard that owns every
`*.Tests.Integration` project, plus the `architecture` shard. Each shard has a
**solution filter** (`.slnf`) that builds only the required subset of projects —
no full-solution rebuild per shard.

Shard definitions: `.github/test-shards.json` (source of truth).
Solution filters: `.github/shard-filters/*.slnf` (auto-generated).

**When creating a new test project:**

1. Add its directory to the correct shard in `test-shards.json` —
   **`*.Tests.Integration` projects ALWAYS go in the `integration` shard**,
   regardless of the domain layer. All other tests go in their domain shard.
2. Run `python3 scripts/generate-shard-filters.py` to regenerate `.slnf` files
3. Commit both `test-shards.json` and `.github/shard-filters/*.slnf`

The pre-push hook auto-regenerates filters when `.csproj` or `test-shards.json`
files change and folds the result into HEAD via `git commit --amend`.

Shard mapping:

| Shard | Layer | Modules |
| ----- | ----- | ------- |
| `core-ai` | Core + AI | Core, Validation, Analyzers, Diagnostics, Observability, Timing, Guids, Testing, AI.* |
| `business` | Business Features | Workflow, DataExchange, Templating, DocumentGeneration, Timeline, QueryEngine, ReferenceData, Parties |
| `api-data` | API & Http + Data | Http.*, BlobStorage, Persistence, Caching, Imaging, RateLimiting, Webhooks |
| `infrastructure` | Infrastructure | Notifications, BackgroundJobs, Wolverine, Localization, Settings, Features, MultiTenancy, EventBus |
| `security` | Security & Compliance | Auditing, Authentication, Authorization, Identity, Vault, Encryption, Privacy, Security |
| `saas` | SaaS | Invoicing, Subscriptions, CustomerBalance, Tax, Payments |
| `integration` | Integration Tests | All `*.Tests.Integration` projects (Postgres / Testcontainers — isolated shard so unit shards are not held back by container spin-up) |
| `architecture` | Architecture Tests | ArchitectureTests (references all src projects — isolated shard) |

**NEVER** create a test project without adding it to a shard — the CI will silently skip it.

## Anti-patterns — NEVER do this

Code anti-patterns are the inverse of conventions above. Key additional rules:

### Code (not covered above)

- `new HttpClient()` → `IHttpClientFactory`
- `async void` → always return `Task`
- `.Result` / `.Wait()` → `await`
- Bare `catch (Exception)` → catch specific types

### Architecture

- Merge `I*Reader`/`I*Writer` into `I*Store` → CQRS, keep separate
- Share DbContext across modules → isolated DbContext per module
- Cross-module direct method calls → integration events (Wolverine)
- Repository pattern over EF Core → use DbContext directly

### Refactoring

See global `~/.claude/CLAUDE.md` for base rules. Additional Granit-specific:

- Remove/change interface implementations on `ValueObject`/`Entity`/`AggregateRoot` for SonarQube → mark as won't fix
- Reduce constructor params via wrapper types that aren't real domain concepts → mark `brain-overload` as won't fix

### Git — CRITICAL

- **NEVER `git push` to a PR branch without verifying the PR is still open first.**
  Run `gh pr view <number> --json state -q .state` immediately before every `git push`.
  If result is NOT `"OPEN"`, do NOT push. Instead: fetch develop, create a new branch,
  cherry-pick changes, create a new PR. This is a **BLOCKING** check — never skip it.

## Compliance

1. **GDPR**: Minimization, right to erasure, pseudonymization
2. **ISO 27001**: Audit trail, encryption at rest and in transit
3. **Secrets**: No plaintext secrets, mandatory rotation

## Localization

See [`docs/guide/conventions/langues.md`](docs/guide/conventions/langues.md) for full rules.

18 cultures — 15 base (en, fr, nl, de, es, it, pt, zh, ja, pl, tr, ko, sv, cs, hi) + 3 regional (fr-CA, en-GB, pt-BR). Every `src/*/Localization/**/*.json` must exist for all 18. Regional files only contain differing keys.

## Documentation site

The docs live in `docs-site/` (Astro + Starlight). Key paths:

| Path | Content |
| ---- | ------- |
| `docs-site/src/content/docs/reference/modules/` | .NET module reference (one `.mdx` per module) |
| `docs-site/src/content/docs/reference/frontend/` | Frontend SDK reference |
| `docs-site/src/content/docs/architecture/patterns/` | Design patterns |
| `docs-site/src/content/docs/architecture/adr/` | ADRs |
| `docs-site/src/data/constants.ts` | Counters (update when adding packages/patterns/ADRs) |

**When creating a new module**: create `.mdx` in `reference/modules/`, update `PACKAGE_COUNT` in `constants.ts`, add "See also" links from related pages.

## MCP & Code index

MCP tools (roslyn-lens + granit-tools) — see global `~/.claude/CLAUDE.md`.

`.mcp-code-index.json` is auto-regenerated by the **pre-push** hook on `.cs`/`.csproj`
changes, then folded into HEAD via `git commit --amend` so it ships with the push.
Script: `python3 scripts/generate-code-index.py`. **NEVER edit manually.**

## Definition of Done

See [`docs/guide/conventions/dod.md`](docs/guide/conventions/dod.md)

**NEVER push or create an MR** without: tests passing, docs updated, `dotnet format --verify-no-changes`, markdownlint clean. These are **blocking**. Refuse until satisfied or user explicitly overrides.
