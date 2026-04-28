---
title: "ADR-039: Widget renderer architecture"
description: "Each WidgetType ships a typed IWidgetSource<TSnapshot> (per EPIC #1366 invariant #1). A non-generic IWidgetInstanceRenderer adapter sits in front for runtime dispatch by WidgetType, keeping per-kind source code typed end-to-end while still allowing a single dashboard render endpoint to compose heterogeneous widgets."
sidebar:
  order: 39
  label: "039 - Widget Renderer Architecture"
---

> **Date:** 2026-04-29
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Dashboards, Granit.Dashboards.Endpoints, Granit.Analytics, Granit.Analytics.Endpoints)
> **Epic:** [#1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence
> **Stories:** [#1384](https://github.com/granit-fx/granit-dotnet/issues/1384) (B3 widget kinds) · [#1385](https://github.com/granit-fx/granit-dotnet/issues/1385) (B4 render endpoint) · [#1404](https://github.com/granit-fx/granit-dotnet/issues/1404) (B7 MapWidget)

## Context

The B4-write surface for dashboards has shipped (catalogue, import, list, read,
state transitions, metadata edit, widget CRUD). Two adjacent stories remain
blocked on a common architectural decision:

- **B3** (#1384) — five typed widget kinds (Kpi / Chart / Table / Pivot /
  Markdown, plus the recently-shipped declarative `MapWidgetDefinition` from
  B7-1) need server-side renderers that turn a persisted `WidgetInstance` into
  a snapshot the frontend can render.
- **B4-render** — the last B4 endpoint, `POST /dashboards/{id}/render`, has to
  iterate the dashboard's widget pool, dispatch each widget to its renderer,
  apply per-widget permission filtering, and return one bundled payload whose
  schema matches EPIC #1366 invariant #8.

Feature A (#1367 — inline metrics) already shipped two of the three runtime
primitives the renderer needs:

- `IWidgetSource<TSnapshot>` (`Granit.Analytics/Widgets/IWidgetSource.cs`) — per-kind
  contract with `RenderAsync` (pull) and `SubscribeAsync` (push, channel-based,
  default emulation). Locked v1 per EPIC invariant #1.
- `WidgetPayload<TSnapshot>(Snapshot, Sequence, EmittedAt, RefreshHint)`
  (`Granit.Analytics/Widgets/WidgetPayload.cs`) — canonical envelope that
  composes with future `{ Sequence, Delta }` push messages. Locked v1 per
  EPIC invariant #2.

`MetricEndpointService` (`Granit.Analytics.Endpoints/Internal/`) already does
the inline-metric pipeline: tenant resolution, period parsing, FusionCache,
delta calculation against a comparison window. The KPI widget renderer should
delegate to it — *no duplication, direct reuse* per B3 acceptance criteria.

What's missing is the **dispatcher**: how the dashboard render endpoint, given
a heterogeneous `Dashboard.Widgets` list, picks the right `IWidgetSource<T>`
implementation per `WidgetInstance.WidgetType` without leaking generics.

## Decision

Three layers, each owning one concern.

### 1. Per-kind typed source — `IWidgetSource<TSnapshot>` (already exists)

Unchanged. Every widget kind has exactly one implementation:

| WidgetType | Implementation | TSnapshot |
| ---------- | -------------- | --------- |
| `Kpi` | `KpiWidgetSource` | `KpiSnapshot` (delegates to `MetricSnapshotPayload`) |
| `Chart` | `ChartWidgetSource` | `ChartSnapshot { Series, Buckets }` |
| `Table` | `TableWidgetSource` | `TableSnapshot { Rows, Total }` |
| `Pivot` | `PivotWidgetSource` | `PivotSnapshot { Rows, Columns, Cells }` |
| `Map` | `MapWidgetSource` | `MapSnapshot { Points, Bounds, InvalidCount }` |
| `Markdown` / `Text` | `MarkdownWidgetSource` / `TextWidgetSource` | `TextSnapshot { Content }` |
| `Image` | `ImageWidgetSource` | `ImageSnapshot { Url, AltLocalizationKey }` |

Each source closes over its `WidgetInstance` + render context at construction
time, then exposes `Task<WidgetPayload<TSnapshot>> RenderAsync(...)`. Stays
fully typed — no `object?` in the source's body.

### 2. Non-generic dispatch adapter — `IWidgetInstanceRenderer`

A new abstraction in `Granit.Dashboards.Abstractions`:

```csharp
public interface IWidgetInstanceRenderer
{
    /// <summary>
    /// The widget-type discriminator this renderer handles. Matches
    /// <see cref="WidgetInstance.WidgetType"/> exactly (case-sensitive) and
    /// <see cref="WidgetDefinition"/>'s <c>"type"</c> JSON discriminator (P1.1).
    /// </summary>
    string WidgetType { get; }

    /// <summary>
    /// Renders the widget. Returns a non-generic envelope so the dispatcher
    /// can bundle heterogeneous widgets into one response without leaking
    /// per-kind generics. Permission filtering and error handling are the
    /// dispatcher's responsibility — see <see cref="WidgetSnapshotStatus"/>.
    /// </summary>
    Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken);
}

public sealed record WidgetSnapshotEnvelope(
    WidgetSnapshotStatus Status,                    // runtime outcome — never the widget kind
    string WidgetType,                              // wire discriminator — "Kpi", "Chart", "Markdown", …
    JsonElement? Snapshot,                          // pre-serialised — see §2.1
    long Sequence,
    DateTimeOffset EmittedAt,
    RefreshHint RefreshHint,
    string? UnavailableReasonLocalizationKey = null);

public enum WidgetSnapshotStatus
{
    Snapshot,       // Snapshot is non-null, the typed payload for the kind
    Unavailable,    // Permission denied — UnavailableReasonLocalizationKey is set
    Error,          // Renderer threw an unexpected exception — Snapshot is null
}
```

The envelope splits two concerns the early draft accidentally collapsed onto a
single `kind` field:

- **`Status`** — runtime outcome (`Snapshot` / `Unavailable` / `Error`). Drives
  the frontend's "render the snapshot" vs "render a placeholder" branch.
- **`WidgetType`** — declarative type (`Kpi` / `Chart` / `Markdown` / `Map` /
  …). Mirrors the `[JsonDerivedType]` discriminator on
  `WidgetDefinition` (P1.1) so the frontend's TypeScript discriminated union
  matches by string compare. Carried even on `Unavailable` / `Error` so the
  UI can keep a typed slot for the absent widget instead of an opaque
  placeholder.

Each kind ships its `IWidgetInstanceRenderer` adapter alongside its
`IWidgetSource<TSnapshot>`. The adapter:

- Builds the typed source from the `WidgetInstance` + `WidgetRenderContext`.
- Calls `source.RenderAsync(...)`.
- **Materialises the typed snapshot into a `JsonElement` at the boundary** via
  `JsonSerializer.SerializeToElement(payload.Snapshot, options)` — see §2.1
  below.
- Wraps the resulting `JsonElement` into the non-generic
  `WidgetSnapshotEnvelope`.

The adapter is the **only** non-generic edge. Frontend wire schema stays per
EPIC #1366 invariant #8 — the dashboard render endpoint serialises the
envelopes into `{ widgets: [{ id, sequence, refreshHint, snapshot, kind }] }`.

#### 2.1 Serialization boundary — `JsonElement?`, not `object?`

The `Snapshot` field on `WidgetSnapshotEnvelope` is intentionally typed
`JsonElement?`, not `object?`. Two reasons:

1. **`System.Text.Json` serialises `object` based on the declared type, not the
   runtime type.** A field declared `object? Snapshot` containing a
   `KpiSnapshot` instance serialises as `{}` unless every call site remembers
   to pass `value.GetType()` to `JsonSerializer.Serialize`. The
   `IDashboardRenderer` aggregates heterogeneous envelopes into one response;
   forgetting that on any of N rendering paths is a silent payload-emptying
   bug. Forcing the typed → JSON conversion *at the adapter* makes the
   boundary explicit and impossible to forget.
2. **The downstream pipeline only handles JSON anyway.** The endpoint serialises
   `DashboardRenderResponse`, the FusionCache stores cache entries as JSON,
   the future WS push transport will multiplex `JsonElement`-shaped delta
   messages. Doing the conversion once in the adapter (when the typed payload
   is fresh on the stack) is strictly cheaper than letting it propagate
   through generic-erased code paths.

The adapter pattern collapses to:

```csharp
internal sealed class KpiWidgetInstanceRenderer(
    KpiWidgetSourceFactory factory,
    JsonSerializerOptions json) : IWidgetInstanceRenderer
{
    public string WidgetType => "Kpi";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        IWidgetSource<KpiSnapshot> source = factory.Create(widget, context);
        WidgetPayload<KpiSnapshot> payload = await source.RenderAsync(cancellationToken).ConfigureAwait(false);

        // Boundary conversion — KpiSnapshot is serialised by its CONCRETE type
        // here, not by `object`. Downstream code only sees the JsonElement.
        JsonElement snapshot = JsonSerializer.SerializeToElement(payload.Snapshot, json);

        return WidgetSnapshotEnvelope.Snapshot(
            snapshot,
            payload.Sequence,
            payload.EmittedAt,
            payload.RefreshHint);
    }
}
```

A small `WidgetSourceAdapter<TSource, TSnapshot>` generic helper can hide the
three-line ceremony so per-kind adapters stay ~5 lines after B3-3.

### 3. Registry + dashboard renderer — `IDashboardRenderer`

A keyed dispatcher resolved from DI:

```csharp
internal sealed class DashboardRenderer(
    IEnumerable<IWidgetInstanceRenderer> renderers,
    IPermissionEvaluator permissions,
    IClock clock) : IDashboardRenderer
{
    private readonly IReadOnlyDictionary<string, IWidgetInstanceRenderer> _byType =
        renderers.ToDictionary(r => r.WidgetType, StringComparer.Ordinal);

    public async Task<DashboardRenderResponse> RenderAsync(
        Dashboard dashboard,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        List<WidgetSnapshotEnvelope> results = new(dashboard.Widgets.Count);
        foreach (WidgetInstance widget in dashboard.Widgets.OrderBy(w => w.Position))
        {
            // 3.a — per-widget permission gate: drop to Unavailable BEFORE the
            //       typed renderer runs, so a widget the user cannot read never
            //       hits the underlying metric / query.
            if (widget.RequiredPermission is { } perm
                && !await permissions.HasAsync(context.User, perm).ConfigureAwait(false))
            {
                results.Add(WidgetSnapshotEnvelope.Unavailable(
                    sequence: 1,
                    emittedAt: clock.Now,
                    refreshHint: RefreshHint.Static,
                    reasonKey: "Widget:Unavailable"));
                continue;
            }

            // 3.b — registered renderer? If a definition references an unknown
            //       WidgetType (module unloaded, version mismatch), the result
            //       is Error, not a 500 — one widget cannot break a whole grid.
            if (!_byType.TryGetValue(widget.WidgetType, out IWidgetInstanceRenderer? renderer))
            {
                results.Add(WidgetSnapshotEnvelope.Error(/* ... */));
                continue;
            }

            // 3.c — typed renderer dispatch. The renderer owns its own caching,
            //       period filtering, and snapshot computation.
            try
            {
                WidgetSnapshotEnvelope envelope = await renderer
                    .RenderAsync(widget, context, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(envelope);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)                 // logged via ILogger — see §Observability
            {
                results.Add(WidgetSnapshotEnvelope.Error(/* ... */));
            }
        }

        return new DashboardRenderResponse(
            DashboardId: dashboard.Id,
            RenderedAt: clock.Now,
            Period: context.Period,
            Widgets: [.. results.Select((e, i) => MapToWire(dashboard.Widgets, e, i))]);
    }
}
```

Three guarantees in one place:

- **Per-widget permission filter is uniform.** Every kind inherits it for free —
  individual sources never have to check permissions. Story B4 acceptance
  criterion 4 ("a widget pointing at a metric the user cannot read returns
  `WidgetUnavailable`, the dashboard renders without it, no 403 for the whole
  request") is enforced once.
- **Per-widget error isolation.** A bad config row, an unloaded module, an
  unhandled exception in one renderer doesn't kill the whole render. The
  dashboard always returns 200 with one or more widgets in `Error` /
  `Unavailable` state.
- **Ordering is deterministic.** Widgets stream out in `Position` order
  regardless of which renderer is faster. The frontend reconciles incremental
  push messages later by widget id, not position.

### 4. Render context — `WidgetRenderContext`

```csharp
public sealed record WidgetRenderContext(
    Guid? TenantId,
    ClaimsPrincipal User,
    ResolvedPeriod? Period,
    string Locale,
    IReadOnlyDictionary<string, string> DashboardFilters,
    IReadOnlyDictionary<string, EntityAliasBinding> ResolvedEntityAliases);
```

Built once at the dashboard render endpoint entry and passed unchanged to
every renderer. Locks the same six inputs across kinds, so the cache key
composition (next §) is uniform.

The `ResolvedEntityAliases` dictionary materialises the dashboard's declared
`EntityAlias` list (see `Granit.Dashboards.Abstractions.EntityAlias` —
introduced in P2.3) at render time. The render endpoint runs every registered
`EntityAliasResolver` against the request before invoking renderers and
freezes the results into the context. Telemetry KPIs that reference an alias
(e.g. `TelemetryDatasource.EntityAlias = "currentDevice"`) read the resolved
binding directly from the context — they never touch `IEntityAliasResolver`
themselves, keeping the renderer side stateless and easy to test.

### 5. Cache key composition — matches future WS subscription identity

Per EPIC #1366 invariant #7 — the render-time cache key serves *tel quel* as
the WebSocket subscription topic when push transport lands. Each renderer
composes its own key but follows the same recipe:

```text
widget:{kind}:{tenantId|global}:{widgetInstanceId}:{filterSpecHash}:{period}:{userId?}
```

- `userId` is appended only when the widget is permission-gated below the
  dashboard-level permission (i.e. the widget's own `RequiredPermission` is
  non-null). Two users with the same dashboard permission but different widget
  permissions get separate cache entries.
- `filterSpecHash` is the SHA-256 of the canonicalised filter dictionary —
  short hash, stable across invocations, doesn't leak filter values.
- `period` is the `ResolvedPeriod`'s `[from..to)` string-formatted (UTC), or
  the literal `none` when the widget has no period selector.

KPI widgets reuse `MetricCacheKey.Compose(...)` from the inline-metric path
unchanged — one source of truth for "this metric, this tenant, this period".

### 6. Wire shape — `DashboardRenderResponse`

```jsonc
{
  "dashboardId": "8c6b...",
  "renderedAt": "2026-04-29T12:34:56.789Z",
  "period": { "from": "2026-04-01T00:00:00Z", "to": "2026-04-29T00:00:00Z", "token": "mtd" },
  "widgets": [
    {
      "id": "...",
      "widgetType": "Kpi",
      "status": "Snapshot",
      "sequence": 1,
      "refreshHint": "Dynamic",
      "snapshot": { /* KpiSnapshot — typed per kind */ }
    },
    {
      "id": "...",
      "widgetType": "Markdown",
      "status": "Snapshot",
      "sequence": 1,
      "refreshHint": "Static",
      "snapshot": { "content": "## Quarterly review\n…" }
    },
    {
      "id": "...",
      "widgetType": "Chart",
      "status": "Unavailable",
      "sequence": 1,
      "refreshHint": "Dynamic",
      "snapshot": null,
      "unavailableReasonLocalizationKey": "Widget:Unavailable"
    }
  ]
}
```

Matches EPIC #1366 invariant #8. Two fields disambiguate concerns the early
draft conflated: `widgetType` is the declarative kind discriminator (mirror of
`WidgetDefinition`'s P1.1 `[JsonDerivedType]` `"type"`), `status` is the
runtime outcome (`Snapshot` / `Unavailable` / `Error`).

#### 6.1 Enum casing on the wire — PascalCase by default

The framework's host `JsonStringEnumConverter()` registration uses **no naming
policy**, so `RefreshHint`, `WidgetSnapshotStatus`, `MetricValueKind`,
`ChartType`, etc. all serialise as PascalCase (`"Dynamic"`, `"Snapshot"`,
`"Count"`, `"Line"`). The example above intentionally reflects this — frontend
TypeScript types must match exactly (`type RefreshHint = "Static" | "Dynamic"
| "Realtime"`).

Adopting `JsonNamingPolicy.CamelCase` retroactively would be a wire-format
break on every existing endpoint. Stay PascalCase here.

`PeriodSpec.Token` is a `string?`, **not** an enum, so the
`JsonStringEnumConverter` rule does not apply to it. Period tokens follow the
documented analytics-conventions lowercase set
(`today`, `yesterday`, `last_7d`, `last_30d`, `mtd`, `qtd`, `ytd`,
`previous_period`, …) — that's why the example payload has
`"token": "mtd"`, lowercase, alongside `"refreshHint": "Dynamic"`,
PascalCase. Different shapes by design.

#### 6.2 Frontend reconciliation — per-widget TanStack cache entries

The dashboard render endpoint returns one bundle, but the frontend
`useDashboard` hook MUST split the payload into one TanStack Query entry per
widget (`['dashboard', dashboardId, 'widget', widgetId]`) before storing it.
This single decision unlocks two future invariants:

- **Push-message reconciliation.** When a future SSE / WebSocket transport
  emits `{ widgetId, sequence, delta }`, the hook updates only the matching
  cache entry. No global refetch, no cross-widget invalidation cascade.
- **Independent staleness per widget.** Static-hint widgets stay fresh for
  5 min; dynamic widgets refresh at 60–120 s. Per-widget cache entries let
  TanStack apply the right TTL per kind without recomputing the whole
  dashboard on the first refresh tick.

The bundle response is a *transport optimisation* (one HTTP round-trip over
N widgets), not the cache identity. Frontends that bypass `useDashboard` and
store the full bundle as a single cache entry will need to refactor when
push lands.

### 7. Discovery + DI registration

Each `*.{Module}.Endpoints` (or `*.Analytics`) package registers its renderers
via the standard convention — same shape as the existing notification-channel
pattern:

```csharp
services.AddSingleton<IWidgetInstanceRenderer, KpiWidgetInstanceRenderer>();
services.AddSingleton<IWidgetInstanceRenderer, ChartWidgetInstanceRenderer>();
services.AddSingleton<IWidgetInstanceRenderer, TableWidgetInstanceRenderer>();
services.AddSingleton<IWidgetInstanceRenderer, PivotWidgetInstanceRenderer>();
services.AddSingleton<IWidgetInstanceRenderer, MarkdownWidgetInstanceRenderer>();
// …
```

The `DashboardRenderer` resolves them via `IEnumerable<IWidgetInstanceRenderer>`.
Adding a new widget kind is one new registration plus a new
`IWidgetInstanceRenderer` implementation — no central switch statement, no
modification to `Granit.Dashboards.Endpoints`.

An architecture test will assert that every concrete `WidgetDefinition` shipped
in any loaded assembly has exactly one matching `IWidgetInstanceRenderer`
registered, so a kind that ships in `Granit.Analytics` without its renderer in
`Granit.Analytics.Endpoints` fails the build.

### 7.bis. KPI renderer — dispatch on `Datasource.Kind`

The `KpiWidgetInstanceRenderer` is **not** a thin adapter over
`MetricEndpointService`. Since P2.2 (PR #1464), `KpiWidgetDefinition.Datasource`
is the polymorphic abstraction `MetricDatasource | QueryAggregateDatasource |
TelemetryDatasource`. Hard-coding the metric path would silently regress IoT
gauges and ad-hoc query-aggregate KPIs — both shipped contracts.

The KPI renderer therefore dispatches on `Datasource.Kind`:

```csharp
internal sealed class KpiWidgetInstanceRenderer(
    IDatasourceEvaluator<MetricDatasource> metricEvaluator,
    IDatasourceEvaluator<QueryAggregateDatasource> queryEvaluator,
    IDatasourceEvaluator<TelemetryDatasource> telemetryEvaluator,
    JsonSerializerOptions json) : IWidgetInstanceRenderer
{
    public string WidgetType => "Kpi";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget, WidgetRenderContext ctx, CancellationToken ct)
    {
        Datasource datasource = ParseConfigDatasource(widget.ConfigJson);

        KpiSnapshot snapshot = datasource switch
        {
            MetricDatasource m         => await metricEvaluator.EvaluateAsync(m, widget, ctx, ct).ConfigureAwait(false),
            QueryAggregateDatasource q => await queryEvaluator.EvaluateAsync(q, widget, ctx, ct).ConfigureAwait(false),
            TelemetryDatasource t      => await telemetryEvaluator.EvaluateAsync(t, widget, ctx, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown KPI datasource '{datasource.GetType().Name}'."),
        };

        return WidgetSnapshotEnvelope.Snapshot(
            JsonSerializer.SerializeToElement(snapshot, json),
            sequence: 1,
            emittedAt: ctx.Clock.Now,
            refreshHint: snapshot.RefreshHint,
            widgetType: WidgetType);
    }
}

public interface IDatasourceEvaluator<TDatasource> where TDatasource : Datasource
{
    Task<KpiSnapshot> EvaluateAsync(
        TDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken);
}
```

Concrete evaluators ship per data source:

- **`MetricDatasourceEvaluator`** in `Granit.Analytics.Endpoints` —
  delegates to the existing `MetricEndpointService` (no duplication).
- **`QueryAggregateDatasourceEvaluator`** in `Granit.Analytics.Endpoints` —
  builds an `IQueryable<TEntity>` from the named `QueryDefinition`, applies
  the dashboard filter spec + period selector, runs the declared
  `AggregateFunction`. Empty-set semantics shared with the metric path
  (`Sum/Count → 0`, `Avg/Min/Max → null`).
- **`TelemetryDatasourceEvaluator`** in `Granit.IoT.Dashboards` (deferred,
  outside `granit-dotnet`) — resolves
  `TelemetryDatasource.EntityAlias → deviceId` via
  `WidgetRenderContext.ResolvedEntityAliases`, then queries the IoT
  telemetry store.

The framework ships only the first two; the third is a stub that returns
`UnsupportedDatasourceKind` until the IoT package lands. The arch test from
§7 ignores `IDatasourceEvaluator<T>` registrations — pairing is enforced
*per `WidgetDefinition` ↔ `IWidgetInstanceRenderer`*, not per datasource kind.

## Alternatives considered

### A. Single non-generic `IWidgetSource` + `object` payloads

Drop the typed `IWidgetSource<TSnapshot>` and have every renderer return
`object`/`JsonElement`. Eliminates the adapter layer.

**Rejected.** EPIC invariant #1 explicitly locks `IWidgetSource<TSnapshot>` v1
so that *each kind's source code stays typed* and the future push transport
can swap in a real `SubscribeAsync` without touching call sites. Untyping the
contract today forces every push consumer to re-type at the boundary, defeating
the locked invariant. The adapter layer is the small price for end-to-end
typing.

### B. Visitor / double-dispatch on `WidgetDefinition`

Define `void Accept<TVisitor>(TVisitor visitor) where TVisitor : IWidgetVisitor`
on `WidgetDefinition` and have the dashboard renderer be the visitor.

**Rejected.** Forces every new widget kind to touch the visitor interface —
violates open-closed. Modules that ship their own kinds (e.g. `Granit.IoT`'s
future gauges) couldn't add a renderer without modifying the central
`IWidgetVisitor`. Registry dispatch keyed by `WidgetType` discriminator scales
better and matches how `WidgetDefinition` JSON polymorphism already works
(stable string discriminator, runtime registration).

### C. Pre-resolve the `IWidgetSource<T>` at registration time, dispatch by `Type`

`Dictionary<Type, IWidgetSource>` keyed by closed generic type, casted at
render time.

**Rejected.** Same observable behaviour as the adopted design but worse
ergonomics: `Dictionary<Type, object>` with `(IWidgetSource<TSnapshot>)cast`
inside hot paths. Strings are cheaper as keys, match the wire discriminator
exactly, and surface in logs without `Type.FullName` noise.

### D. Inline rendering inside the endpoint handler

Skip the `IDashboardRenderer` abstraction; write a `switch (widget.WidgetType)`
in the endpoint method body.

**Rejected.** Couples `Granit.Dashboards.Endpoints` to every concrete kind,
re-introduces the central switch the registry pattern avoids, and makes the
permission-filter / error-isolation guarantees a per-handler responsibility
instead of a uniform pipeline.

## Consequences

### Positive

- **Per-kind code stays typed end-to-end.** `KpiWidgetSource` returns
  `WidgetPayload<KpiSnapshot>` with no `object?` casts; the adapter is the only
  place that sheds the generic.
- **Adding a kind is additive.** New `IWidgetInstanceRenderer`, new DI
  registration. No central switch, no abstract method bump, no breaking change
  to consumers.
- **Permission filter / error isolation centralised.** B4 acceptance criterion
  4 is one method on `DashboardRenderer`, not a per-source obligation. A
  third-party kind cannot accidentally bypass the gate.
- **Cache key recipe shared.** Same composition rule across kinds → future
  WS subscription topic lands without per-kind retrofit.
- **Architecture-test enforceable.** Every `WidgetDefinition` MUST have a
  matching renderer registered — drift caught at CI time.

### Negative

- **One extra type per kind.** The adapter doubles the file count
  (`KpiWidgetSource.cs` + `KpiWidgetInstanceRenderer.cs`). The split is
  justified by the contract layering but the boilerplate is real — a
  `WidgetSourceAdapter<TSource, TSnapshot>` generic helper can flatten the
  per-kind adapter to ~10 lines.
- **Non-generic envelope means per-kind tests deserialise via the wire schema.**
  Unit tests for the dashboard render endpoint can't easily access the typed
  snapshot — they go through `WidgetSnapshotEnvelope.Snapshot as KpiSnapshot`.
  Mitigated by ensuring per-kind `IWidgetSource<T>` tests stay typed (the
  source is the unit of test, the adapter is integration-level).

### Neutral

- **Frontend wire schema unchanged from EPIC invariant #8.** Existing TS
  discriminated unions (`type` field on each widget snapshot) remain the
  authoritative contract.
- **Sequence stays 1 in pull mode.** Push transport will increment monotonically
  per widget instance + tenant, but the schema admits both today.

## Implementation slices

The ADR unblocks the following stories, each shippable independently:

1. **B3-1** — `IWidgetInstanceRenderer` + `WidgetSnapshotEnvelope` +
   `WidgetRenderContext` in `Granit.Dashboards.Abstractions`. Pure contracts,
   no implementation. ✓ archi test "every WidgetDefinition has a renderer".
2. **B3-2** — `KpiWidgetInstanceRenderer` plus `MetricDatasourceEvaluator`
   and `QueryAggregateDatasourceEvaluator`. The renderer dispatches on
   `Datasource.Kind` per §7.bis; both bundled evaluators ship together so
   the analytics flavours of KPI work end-to-end. The
   `TelemetryDatasourceEvaluator` is stubbed (returns `Unavailable`) until
   `Granit.IoT.Dashboards` lands.
3. **B3-3** — `MarkdownWidgetInstanceRenderer` + `TextWidgetInstanceRenderer` +
   `ImageWidgetInstanceRenderer` (no I/O, pure projection of
   `WidgetInstance.ConfigJson`).
4. **B3-4** — `TableWidgetInstanceRenderer` (uses `IQueryableSource<T>` +
   pagination).
5. **B3-5** — `ChartWidgetInstanceRenderer` (group-by bucketing —
   `QueryDefinition` group-by extension lands here).
6. **B3-6** — `PivotWidgetInstanceRenderer` (Postgres integration test).
7. **B7-2** — `MapWidgetInstanceRenderer` (LatLng + PostGIS opt-in).
8. **B4-render** — `IDashboardRenderer` + `POST /dashboards/{id}/render`
   endpoint. Composes the above.

Slices 1–3 don't depend on the others. Slices 4–6 share the
`QueryDefinition`-group-by extension and may bundle. Slice 8 needs at least
one renderer in place to be testable end-to-end (KPI is enough).

## References

- [EPIC #1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence (invariants #1, #2, #7, #8 quoted above)
- [Story #1384](https://github.com/granit-fx/granit-dotnet/issues/1384) — B3 widget kinds
- [Story #1385](https://github.com/granit-fx/granit-dotnet/issues/1385) — B4 dashboard endpoints (render endpoint clause)
- [Story #1404](https://github.com/granit-fx/granit-dotnet/issues/1404) — B7 MapWidget
- [ADR-038](/dotnet/architecture/adr/038-analytics-dashboard-definition-vs-aggregate/) — `DashboardDefinition` vs persisted `Dashboard` boundary
- [`IWidgetSource.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics/Widgets/IWidgetSource.cs) — locked v1 contract
- [`WidgetPayload.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics/Widgets/WidgetPayload.cs) — locked v1 envelope
