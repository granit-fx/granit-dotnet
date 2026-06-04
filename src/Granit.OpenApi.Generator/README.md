# Granit.OpenApi.Generator

Build-only tool that emits one **OpenAPI 3.1 document per module** for the Granit framework.
It composes every `Granit.*.Endpoints` module through the Granit module system and writes the
documents at build time — **no running host, no infrastructure**. The artifacts are consumed by
`granit-front` (via `openapi-typescript`) as the per-module API contract.

> Not a NuGet package (`IsPackable=false`). Lives in the `src/Tooling` solution folder.

## Run

```bash
dotnet build src/Granit.OpenApi.Generator                 # → ./generated/Granit.OpenApi.Generator_<slug>.json
dotnet build src/Granit.OpenApi.Generator --no-incremental # force regeneration
```

Do **not** `dotnet run` it — that would start the web host instead of generating. Generation is
driven by `Microsoft.Extensions.ApiDescription.Server` + `OpenApiGenerateDocuments=true`; the
build invokes `GetDocument.Insider`, which launches the entry point with an inert (mock) server.

Output lands in [`generated/`](generated/), which is **git-ignored** — the JSON is generated, not
source. CI builds the generator and publishes the documents for downstream consumers.

## How it works

`GetDocument.Insider` runs `Host.StartAsync()`, so all startup logic executes. To keep generation
config-free and infra-free, three service categories are stripped before `Build()` (the document
is built from endpoint metadata, so none are needed):

- `IHostedService` — DB/Vault/Wolverine/seeder workers
- `IStartupValidator` — every `ValidateOnStart()`
- `IValidateOptions<>` — map-time validators (e.g. `MapGranitBff` reads validated options at map time)

Each module's routes are mounted under `app.MapGroup("").WithGroupName(slug)`, and one document
per slug is registered via `AddGranitOpenApiDocument` (from `Granit.Http.ApiDocumentation`) with
`ShouldInclude = d => d.GroupName == slug` and the full Granit transformer chain.

## Adding a module

`GeneratorEndpoints.All` and `GeneratorModule`'s `[DependsOn]` list must both include every
`Granit.*.Endpoints` module. `OpenApiGeneratorCompletenessTests` (in `Granit.ArchitectureTests`)
fails the build if one is missing.

## Notes

- **Integers**: int32 is normalized to `integer`; int64 keeps the `integer | string` union (JS
  `Number` precision tops out at 2^53). Consumers must type int64 fields as `string | number`.
- **BFF** maps one route group per configured `Bff:Frontends` entry, so [`appsettings.json`](appsettings.json)
  ships a stub frontend — otherwise BFF emits zero paths.
- **Wolverine**: `Granit.Privacy` declares Wolverine sagas with `WolverineFx` `PrivateAssets=all`,
  so this project references `WolverineFx` (private) for reflection over the composed graph to resolve.
