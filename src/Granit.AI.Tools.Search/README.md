# Granit.AI.Tools.Search

Search (RAG) tools for the Granit agentic chat (ADR-067). Exposes opted-in semantic collections
and full-text indexes as ACL-bound `search_{name}` tools that return ranked snippets the agent
can ground its answer on.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Tools.Search
```

## Module registration

Declare the module on your host module so its abstractions are wired before the `AddSearch`
registration below runs:

```csharp
[DependsOn(typeof(GranitAIToolsSearchModule))]
public sealed class MyHostModule : GranitModule { }
```

## Usage

The search builder extends the `AddGranitAITools` flow (which requires `GranitAIToolsModule` via
`[DependsOn]`):

```csharp
services.AddGranitAITools(tools => tools.AddSearch(s =>
{
    // semantic collection (returns similarity scores)
    s.AddSemantic("docs", collectionName: "documentation", description: "Product docs");

    // full-text index (tenant + ACL bounded via ISearchResultAuthorizer)
    s.AddFullText<Guid, ArticleHit>("articles", hit => hit.Snippet, hit => hit.Id.ToString());
}));
```

Semantic corpora are tenant-scoped by the vector provider; full-text corpora additionally apply
per-record ACL through the registered `ISearchService` authorizer. Bounds (`DefaultLimit`,
`MaxLimit`) are configured under `AI:Tools:Search`.

## Dependencies

- `Granit.AI.Tools`
- `Granit.AI.VectorData`
- `Granit.Indexing`

## Documentation

See the [full documentation](https://granit-fx.dev).
