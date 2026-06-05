# Granit.OpenApi.Generation

Shared, build-time **OpenAPI contract generator** for every Granit bounded context — the framework
and every downstream repo (`granit-website`, `granit-iot`, …). It owns the doc-gen mechanics every
per-repo `*.OpenApi.Generator` would otherwise copy-paste, so each generator declares only what is
specific to its modules.

## Usage

Drive it from a `Microsoft.NET.Sdk.Web` project with `OpenApiGenerateDocuments=true`. The entry
point is a single call:

```csharp
await OpenApiContractGenerator.RunAsync<GeneratorModule>(args, GeneratorEndpoints.All);
```

- **`OpenApiContractGenerator.RunAsync<TRootModule>(args, modules, configure?)`** — composes the
  modules through the Granit module system and emits one OpenAPI 3.1 document per module. It strips
  `IHostedService` / `IStartupValidator` / `IValidateOptions<>` so `Host.StartAsync()` is a no-op and
  no module reaches for real infrastructure (DB / Vault / object store / message bus).
- **`OpenApiContractModule(string Slug, Action<IEndpointRouteBuilder> Map)`** — one generated
  document: a slug and the routes mounted under it.
- **`AddContractServiceStubs(params Type[])`** (in `.Extensions`) — last-resort escape hatch for
  application-service interfaces a contract-only generator cannot compose. Prefer `[FromServices]` at
  the injection site (enforced by `GRAPI003`); the goal is to need no stubs.

## Why a package

A contract-only generator composes only the `.Endpoints` layer, so this helper must touch
`Microsoft.AspNetCore.*` and the Granit module system — concerns that do not belong inside a focused
runtime package like `Granit.Http.ApiDocumentation`. Shipping it standalone lets downstream repos
reuse the mechanics without re-discovering the doc-gen quirks (infra neutralization, per-module
slicing, the minimal-API binding gap).

See `Granit.OpenApi.Generator` for the framework's own generator built on this helper.
