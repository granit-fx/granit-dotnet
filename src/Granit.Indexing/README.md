# Granit.Indexing

Horizontal full-text + semantic search framework for Granit. Pluggable backends, language-aware analysis, exponential over-fetch with consumer-supplied authorization, and GDPR Art. 17 hooks.

## What's in this package

Base contracts only — no backend, no language detector, no AI provider.

- **Write port** — `IIndexer<TKey>` (`IndexAsync`, `RemoveAsync`).
- **Read port** — `ISearchService<TKey, TResult>` (orchestrator) over `ISearchBackend<TKey, TResult>` (paged fetch).
- **Authorization boundary** — `ISearchResultAuthorizer<TKey>` extensibility port for per-resource ACL filtering. The framework enforces tenant isolation only; everything else is the consumer module's responsibility.
- **Enrichment ports** — `ILanguageDetector` (+ `CompositeLanguageDetector`), `ISummarizer`, `IAutoTagger` (+ `ITagCandidateProvider`).
- **Data subject hook** — `IIndexedEntrySource<TKey>.GetDataSubjectIdAsync` for GDPR Art. 17 cascades.
- **Events** — `EntryIndexedEvent<TKey>`, `EntryIndexingFailedEvent<TKey>` (local bus).
- **Options** — `GranitIndexingOptions` (`SectionName = "Indexing"`).
- **Diagnostics** — `IndexingMetrics`, `IndexingActivitySource`.

## Companion packages

| Package | Concern |
| --- | --- |
| `Granit.Indexing.EntityFrameworkCore` | Postgres tsvector default backend. |
| `Granit.Indexing.Elasticsearch` | ES backend for OR cluster deployments. |
| `Granit.Indexing.Lingua` | Default `ILanguageDetector` (Apache-2.0). |
| `Granit.Indexing.AI.*` | AI-backed `ISummarizer`, `IAutoTagger`, embedding generators. |
| `Granit.Indexing.BackgroundJobs` | Reindex / cleanup recurring jobs. |

## Authorization boundary — critical contract

`Granit.Indexing` enforces tenant isolation only. Per-resource ACL (workspace ACL, role-based row filtering, public-link grants) lives in the consumer module and plugs in via `ISearchResultAuthorizer<TKey>`.

The orchestrator runs an exponential over-fetch loop: iteration 1 fetches `pageSize × RecommendedInitialMultiplier`, then doubles each iteration until the authorised subset fills the page, the backend is exhausted, or `MaxAuthorizationDepth` is hit (default 5 000). When the ceiling trips, `SearchPage<TResult>.HitAuthorizationLimit` is set to `true` as an aggregated UX hint — **endpoints MUST throttle the hint to at most one display per principal per 60 s**.

Empty-result responses are rate-limited per principal (default 10/min) to slow down existence-oracle probing.
