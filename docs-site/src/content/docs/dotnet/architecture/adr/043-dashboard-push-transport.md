---
title: "ADR-043: Dashboard push transport (SSE / WebSocket) is framework-owned"
description: "Push transport for dashboards is a Granit framework concern, not deferred to a downstream IoT repository. RefreshHint stays widget-level; a new DashboardPushPolicy stays dashboard-level; the effective policy at render time is the union. SSE is the default v1 transport; WebSocket ships as an optional sibling. Renderers, transports, and the existing pull endpoint share one envelope shape."
sidebar:
  order: 43
  label: "043 - Dashboard Push Transport"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Analytics`, `Granit.Dashboards`, `Granit.Dashboards.Endpoints`, new `Granit.Dashboards.Push`)
> **Epic:** [#1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence (P2.4 of the dashboards roadmap)
> **Status:** Proposed

## Context

The BI EPIC #1366 v1 ships pure **pull** transport: `POST /dashboards/{id}/render`
returns a one-shot bundle, the frontend re-fetches per the `RefreshHint`. The wire
shape was deliberately designed to be push-compatible — `WidgetSnapshotEnvelope`
carries a `Sequence` field that increments per `(widget, tenant)`, and
`IWidgetSource<TSnapshot>` declares `SubscribeAsync` returning a
`ChannelReader<WidgetPayload<TSnapshot>>` — but no transport ever shipped.

Several call sites in code and docs frame push transport as *"reserved for the
future `granit-iot` repository"*:

- [`RefreshHint.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics.Abstractions/Metrics/RefreshHint.cs) — `Realtime` value: *"Reserved — requires `granit-iot` transport."*
- [`MetricDefinition.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics/Metrics/MetricDefinition.cs) — *"`Realtime` is reserved — it requires the push transport scheduled in the `granit-iot` repo."*
- [`IWidgetSource.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics/Widgets/IWidgetSource.cs) — *"Real push (WebSocket / SSE) replaces the emulation when the `granit-iot` transport lands."*
- [`MapWidgetDefinition.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics/Dashboards/Widgets/MapWidgetDefinition.cs) — *"a future `granit-iot` dashboard module owns live device traces"*
- [`AnalyticsEndpointsOptions.cs`](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Analytics.Endpoints/Options/AnalyticsEndpointsOptions.cs) — *"push transport — see `granit-iot` roadmap"*

**This is wrong.** Push transport is not an IoT-specific concern — it is a
framework-level capability that any vertical may need:

- A SaaS *Customer Success* dashboard wants live churn-risk events.
- A *Workflow* dashboard wants real-time task-state transitions.
- An *Auditing* dashboard wants a live security-event stream.
- An IoT cockpit wants sensor traces.

Conflating "push transport" with "IoT" forces every non-IoT vertical that
needs live updates to either pull at unhealthy cadences or take an unwanted
dependency on a specialised repo. It also leaves the existing scaffolding
(`Sequence`, `IWidgetSource.SubscribeAsync`, `RefreshHint.Realtime`) in
permanent placeholder state — a code smell that compounds with every
release that doesn't ship the transport.

`granit-iot` will still exist — but its job is to ship **IoT-specific
widget definitions** (sensor gauge, camera feed, alarm light) and the
*producer* side of the transport (MQTT bridge, OPC-UA adapter). The
**transport itself** belongs in `granit-dotnet`.

## Decision

### 1. Push transport is owned by `granit-dotnet`

A new package `Granit.Dashboards.Push` ships the SSE transport and the
sequence-numbering / fan-out plumbing. WebSocket lands as an optional
sibling `Granit.Dashboards.Push.WebSockets` for hosts that want
bidirectional payloads (e.g. live alerting back to the server). Both
implement the same producer contract.

| Package | Role | Default? |
| ------- | ---- | -------- |
| `Granit.Dashboards.Push` | SSE transport, `IWidgetPushPublisher`, sequence allocator, fan-out hub | yes (recommended for v1) |
| `Granit.Dashboards.Push.WebSockets` | WebSocket transport sharing the same publisher contract | opt-in |

### 2. Per-dashboard *and* per-widget — `DashboardPushPolicy` × `RefreshHint`

The user-visible question is *"which widgets warrant a live channel?"*.
Answering it one way or the other (dashboard-only OR widget-only) loses
real cases:

- A *cockpit* dashboard is live by design — every widget should push, even
  ones whose `MetricDefinition.RefreshHint` is `Dynamic` (admin opted in
  for the whole board).
- A *Finance Overview* dashboard is mostly KPIs (`Dynamic`) but carries one
  *alerting widget* whose underlying metric is `Realtime` — only that
  widget should push.

The contract therefore exposes **two orthogonal axes**:

#### 2.1 Per-widget hint — `RefreshHint`, already there

`RefreshHint.Realtime` is no longer "reserved". It declares *"this widget's
data is push-eligible — wire the live channel when the host has loaded the
push transport"*. Inheritance stays the same:

- KPI widgets inherit from `MetricDefinition.RefreshHint`.
- Chart / Table / Pivot inherit from a future `QueryDefinition.RefreshHint`
  (today they hardcode `Dynamic`).
- Static-content widgets (Markdown / Image / Text) stay `Static` — never
  push.

#### 2.2 Per-dashboard policy — `DashboardPushPolicy`, new

`DashboardDefinition` exposes a virtual `PushPolicy` property; the persisted
`Dashboard` aggregate captures the same value at import time and admins
can adjust it on their persisted instance:

```csharp
public enum DashboardPushPolicy
{
    /// <summary>
    /// Pure pull. Dashboard ignores any per-widget Realtime hint. Frontend
    /// polls per RefreshHint TTL. Default for verticals that don't ship the
    /// push transport package or want to keep the cost ceiling predictable.
    /// </summary>
    PullOnly = 0,

    /// <summary>
    /// Push the widgets whose effective RefreshHint is Realtime. Pull
    /// everything else. Default for dashboards that mix live + cached data
    /// (e.g. one alerting widget on a finance overview).
    /// </summary>
    WhenWidgetsRequest = 1,

    /// <summary>
    /// Push every widget regardless of its RefreshHint. Use sparingly —
    /// reserved for cockpit-style boards where the whole admin's mental
    /// model is "this board is live".
    /// </summary>
    Force = 2,
}
```

#### 2.3 Effective policy — composition rule

At render time the framework computes the **effective transport per widget**:

| Dashboard policy | Widget hint | Effective transport |
| ---------------- | ----------- | ------------------- |
| `PullOnly` | any | pull |
| `WhenWidgetsRequest` | `Static` / `Dynamic` | pull |
| `WhenWidgetsRequest` | `Realtime` | push |
| `Force` | `Static` | pull (no point pushing static content) |
| `Force` | `Dynamic` / `Realtime` | push |

The render bundle response surfaces the chosen transport on each
`DashboardRenderedWidgetResponse` so the frontend knows which subscriptions
to open, and which widgets to leave on TanStack pull cadences. Wire field:
`Transport: "pull" | "push"`.

### 3. Wire contract — SSE first

The default transport ships as **Server-Sent Events** under one endpoint per
dashboard:

```http
GET /dashboards/{id}/stream
Accept: text/event-stream
```

Wire format — one event per envelope, JSON-encoded same shape as
`DashboardRenderedWidgetResponse`:

```text
event: snapshot
id: <Sequence>
data: { "widgetId": "...", "sequence": 42, "snapshot": {...}, "transport": "push" }

event: heartbeat
data: {"emittedAt":"2026-04-30T10:00:00Z"}
```

Reasons for SSE-as-default:

- One-way (server → client) matches the dashboard use-case 1:1.
- HTTP/2 multiplexes multiple SSE streams over the same connection — no
  WebSocket per-tab budget pressure.
- Built-in reconnect + `Last-Event-ID` resume. Sequence numbering on the
  server side is enough — clients don't need a custom replay protocol.
- No SignalR / Microsoft.AspNetCore.WebSockets dependency in the default
  package. WebSocket sits behind a sibling package that hosts opt into.
- Auth piggybacks on the existing cookie/JWT — same identity bearer as
  `POST /dashboards/{id}/render`.

WebSocket sibling (`Granit.Dashboards.Push.WebSockets`) ships when a host
wants bidirectional flow (live commands back to the server, e.g.
acknowledging an alert). Same `IWidgetPushPublisher` producer contract,
different consumer protocol.

### 4. Producer contract — `IWidgetPushPublisher`

Modules that produce live data publish through the framework rather than
directly to a SignalR hub:

```csharp
public interface IWidgetPushPublisher
{
    /// <summary>
    /// Publishes a fresh snapshot for the given (tenant, widget). Framework
    /// allocates the next Sequence, fans out to subscribed streams, and
    /// invalidates the FusionCache entry so a cold pull returns the new
    /// value immediately after a push hits.
    /// </summary>
    Task PublishAsync(
        Guid? tenantId,
        Guid widgetInstanceId,
        WidgetSnapshotEnvelope envelope,
        CancellationToken cancellationToken = default);
}
```

The poll-emulation default that `IWidgetSource<TSnapshot>.SubscribeAsync`
documented but never shipped is replaced by the explicit publisher
contract. Producers that *can't* push (because their data only updates on
schedule) simply don't call `IWidgetPushPublisher` — the dashboard falls
back to the pull cadence implied by the effective policy.

### 5. Sequence numbering

Locked by EPIC #1366 invariant #2 — *"`Sequence` is monotonic per `(widget,
tenant)`; v1 in pull mode = always `1`, push transport increments on every
emit"*. The framework owns the allocator:

- v1 implementation: `IDistributedCache`-backed counter, key
  `granit:dashboards:push:seq:{tenantId}:{widgetId}`.
- Pull endpoint stays at `Sequence = 1` (same envelope, no change to the
  bundle response) — pull doesn't need ordering.
- Push transport reads `Last-Event-ID` on reconnect and replays the
  per-widget snapshot at `seq + 1` from a small server-side ring (last 100
  envelopes per widget), then continues live. Ring size is a host option
  with a sensible default — not a per-tenant knob.

The pull / push split therefore stays a *transport* detail. The widget
producer doesn't know whether its envelope reached the client over SSE
or via the next pull.

### 6. Authorization

Stream channels enforce the **same permission gate as the render
endpoint**: `Dashboards.Instances.Read` for the dashboard, plus
per-widget `RequiredPermission` for individual widgets. A client lacking a
widget's permission receives `Status = Unavailable` envelopes (mirrors the
pull behaviour) — never silently dropped. Multi-tenant isolation is
enforced at fan-out: `IWidgetPushPublisher.PublishAsync` takes the tenant
id, the hub partitions subscribers per tenant.

### 7. Pull fallback when transport not wired

A host that ships `Granit.Dashboards.Endpoints` *without*
`Granit.Dashboards.Push` continues working — the framework defaults
`DashboardPushPolicy` to `PullOnly` when no `IWidgetPushPublisher` is
registered, regardless of what the descriptor declared. A widget whose
`RefreshHint` is `Realtime` but whose host has no push transport renders
as if it were `Dynamic` (short-TTL pull). The wire response's `Transport`
field reports `"pull"` so the frontend doesn't try to open a stream that
will never connect.

### 8. Cleanup of `granit-iot` references

Every reference framing push transport as a `granit-iot` concern is
rewritten to reflect framework ownership. The `Map` widget specifically
keeps a `granit-iot` mention — but only for the *content* (live device
traces require NetTopologySuite plumbing that belongs in IoT) — never for
the *transport*.

## Consequences

**Positive:**

- Verticals beyond IoT can ship live dashboards without depending on a
  specialised repo. Customer Success, Workflow, Auditing all unlocked.
- The placeholder scaffolding (`Sequence`, `RefreshHint.Realtime`,
  `IWidgetSource.SubscribeAsync`) becomes load-bearing rather than
  permanently aspirational.
- Per-dashboard policy is the right knob to add — it lets admins opt a
  whole board into live mode without touching individual widget
  definitions.

**Negative:**

- New package family to maintain (`Granit.Dashboards.Push`,
  `Granit.Dashboards.Push.WebSockets`).
- Per-tenant ring buffer is an additional infra concern — hosts on
  serverless platforms (no in-memory state across cold starts) need to
  back it with a distributed cache, which the v1 design assumes anyway.
- The render bundle response gains a new `Transport` field — minor wire
  break for clients that auto-generated their TS types and didn't widen
  on extras. Mitigated by shipping the field as an additive optional
  string with a default of `"pull"` for backwards compat.

**Out of scope for this ADR:**

- The producer-side adapters for IoT protocols (MQTT, OPC-UA) — they live
  in `granit-iot` and consume `IWidgetPushPublisher`.
- The frontend `usePushedDashboard` hook and TanStack invalidation
  bridging — owned by `granit-front`.
- Bidirectional commands over WebSocket (e.g. acknowledging an alert
  from the dashboard) — the WebSocket sibling package will spec this in
  a separate ADR if and when it ships.

## Implementation roadmap

This ADR is the foundation for a new mini-EPIC under #1366. Stories
break down as:

1. **P2.4-A — Cleanup + scaffolding** *(this PR)*
   - Strip `granit-iot` framing from XML docs, prose, and ADR text
     (8 sites).
   - Document `RefreshHint.Realtime` as framework-owned.
   - Document `IWidgetSource<TSnapshot>.SubscribeAsync` as framework-owned
     and remove the poll-emulation promise (the producer contract supersedes
     it).
2. **P2.4-B — `DashboardPushPolicy` + `Transport` wire field** *(separate PR)*
   - Add the enum, expose it on `DashboardDefinition` + the persisted
     `Dashboard` aggregate, surface `Transport` on the render bundle.
   - Architecture test: dashboard renderers must compute the effective
     transport per widget per the matrix in §2.3.
3. **P2.4-C — `Granit.Dashboards.Push` SSE transport** *(happy path)*
   - `IWidgetPushPublisher`, `GET /dashboards/{id}/stream`, sequence
     allocator, fan-out hub.
4. **P2.4-C-Auth — Per-widget permission filter on push**
   - `IWidgetPushPublisher` carries the widget's `RequiredPermission`;
     the SSE handler downgrades envelopes to `Unavailable` for
     subscribers that lack the grant (mirrors the render-time gate in
     `DashboardRenderer`). Per-stream permission cache.
5. **P2.4-C2 — `Last-Event-ID` resume + ring buffer** *(reliability)*
   - Per-`(tenantId, dashboardId)` ring buffer of recent envelopes
     (default 100) + stream-level monotonic cursor on the SSE `id:`
     field. Reconnects with `Last-Event-ID` get the missed envelopes
     replayed before going live.
6. **P2.4-D — `Granit.Dashboards.Push.WebSockets`** *(opt-in)*
   - WebSocket consumer over the same producer contract.
7. **P2.4-E — Frontend `usePushedDashboard`** *(`granit-front`)*
   - Reads the `Transport` field, opens streams selectively, falls back
     to TanStack pull when `Transport = "pull"`.

P2.4-A through P2.4-C-Auth are the load-bearing minimum. C2, D, E are
incremental — C2 closes the reconnect-resume reliability gap, D ships
an alternate transport, E lights the front up.
