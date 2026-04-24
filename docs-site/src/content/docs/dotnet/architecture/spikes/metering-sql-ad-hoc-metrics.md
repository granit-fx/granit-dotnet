---
title: "Spike — SQL ad-hoc metric definitions for Granit.Metering"
description: "Investigates the security and sandboxing approach for ORB-style user-defined SQL metric expressions over MeterEvent. Result: DEFER. The 5 built-in aggregation types cover the framework's target B2B SaaS workloads; the safe paths to ship raw SQL all carry costs that exceed the marginal benefit until explicit user demand surfaces."
sidebar:
  order: 1
  label: "Metering — SQL ad-hoc metrics"
---

> **Date:** 2026-04-24
> **Author:** Jean-Francois Meyers
> **Issue:** [#1171](https://github.com/granit-fx/granit-dotnet/issues/1171) (Phase 2 of [EPIC #1155](https://github.com/granit-fx/granit-dotnet/issues/1155) ORB alignment)
> **Status:** Concluded — recommendation **DEFER**

## TL;DR

ORB lets users define a metric as an arbitrary SQL expression evaluated over the
events table at billing time — the most expressive billing primitive on the
market today. This spike investigates whether `Granit.Metering` should match
that surface, and concludes:

- **DEFER**. The five built-in aggregation types
  (`Sum` / `Max` / `Count` / `Last` / `CountDistinct`) cover ≥ 95 % of the
  scenarios that the framework's B2B SaaS target audience has shipped to date.
- The three safe ways to expose raw SQL (parameterised + AST-validated SQL,
  constrained DSL, embedded SQLite re-execution) each have **non-trivial
  implementation cost and ongoing maintenance burden**, with a security failure
  mode that would directly leak per-tenant billing data.
- Re-evaluate when **at least three independent users** request the capability
  for a scenario the existing primitives demonstrably cannot serve. At that
  point, the **constrained DSL** path is the recommended GO route — see
  [Path forward](#path-forward-when-defer-expires).

## Context

### What ORB does

ORB metric definitions accept arbitrary SQL evaluated against an internal events
table. A typical metric reads roughly:

```sql
SELECT SUM(properties->>'bytes_uploaded'::numeric)
FROM events
WHERE properties->>'region' = 'us-east-1'
  AND properties->>'cold_storage' = 'false'
```

This is enormously expressive — it covers conditional aggregation, joins to
reference tables, expression-based grouping, percentile computations, etc. ORB
runs these on a columnar warehouse (probably ClickHouse or Snowflake) where
predicate pushdown and per-tenant partitioning make multi-million-event scans
cheap. They also accept the operational cost of maintaining a SQL parser/
validator for every release.

### What Granit.Metering does today

After Phase 2 (#1158), `MeterDefinition.AggregationType` admits five values:

| Type | Semantics |
| --- | --- |
| `Sum` | `SUM(quantity)` over the period |
| `Max` | `MAX(quantity)` over the period |
| `Count` | `COUNT(*)` over the period |
| `Last` | `quantity` of the latest event in the period |
| `CountDistinct` | `COUNT(DISTINCT metadata.<DistinctProperty>)` over the period (#1166) |

Plus the on-demand recompute (#1167), event soft-deprecation (#1169), and 365-day
backfill (#1168) covered by neighbouring stories. The combined surface handles
flat-rate, per-seat, per-call, and MAU-style metering — the four pricing models
that show up in 95 % of B2B SaaS billing implementations the maintainer has
audited (Granit.Showcase included).

### Why we even consider raw SQL

There are real workloads the existing primitives cannot serve:

- **Conditional aggregation**: "Sum bandwidth, but only for non-cached requests"
  — would require a `WHERE metadata->>'cached' = 'false'` clause.
- **Expression-based grouping**: "Bill differently per region in the same
  meter" — needs `GROUP BY metadata->>'region'`.
- **Reference-table joins**: "Bill by customer tier looked up in another
  table" — needs a join.

For these, the user's current options are:

1. Pre-compute the predicate at ingestion time (spread the metric across N
   `MeterDefinition` rows) — works for low-cardinality grouping, falls over
   when the grouping key is open-ended.
2. Materialise an aggregate themselves and call
   `POST /metering/events/backfill` with the result — works for batch reporting,
   not for real-time quotas.
3. Drop down to raw SQL on the application's own database (Granit isolates the
   `MeteringDbContext` but the application owns the schema and can read it) —
   bypasses the framework, no quota integration, no audit trail.

None of these are great. **But they exist** — the lack of native ad-hoc SQL is
not currently a blocker for any concrete user the maintainer has talked to.

## The eight questions

### 1. Injection model

| Approach | Defense | Cost | Verdict |
| --- | --- | --- | --- |
| **Parameterised values + raw SQL body** | None on the SQL itself; predicate clauses can still drop the tenant filter or join across tables | None — direct passthrough | ❌ unsafe |
| **AST-validated SQL** (parse the user's SQL with e.g. PgSqlParser, walk the tree, reject anything beyond a whitelist of node types) | Rejects DML, DDL, sub-queries, joins outside whitelist, function calls outside whitelist | High — must track every PostgreSQL syntax extension across versions; SQL Server divergence requires a second parser | 🟡 viable but heavy |
| **Whitelisted columns + WHERE-fragment only** (user supplies only the `WHERE` body and the aggregate function from a closed list, framework wraps it) | Same blast radius as AST-validated, simpler implementation | Medium — closed grammar; need to publish the whitelist as part of the public contract | 🟢 safest within "raw SQL" approaches |
| **Constrained DSL** (custom predicate expression + closed list of aggregates, transpiled to `IQueryable<MeterEvent>`) | EF Core LINQ provider does the SQL generation → no string concatenation possible | Medium-high — design + parser + transpiler + docs | 🟢 safest, most composable |
| **Embedded SQLite re-execution** (export the events to an in-memory SQLite DB, run user SQL there) | Total — the user's SQL touches a copy of the data, no path to the production DB | Medium — copy the events to memory per query (10M events ≈ 1.5 GB raw) | 🟡 safe but expensive |

**Decision driver**: the framework's threat model treats per-tenant data
isolation as the strongest invariant (GDPR, ISO 27001 A.9.4). Any approach that
passes user-supplied syntax to the production query planner must
**enumerate** what is allowed (whitelist), never **subtract** what is
forbidden (blacklist). That alone disqualifies the "parameterised raw SQL"
approach.

### 2. Tenant isolation

The `MeteringDbContext` already applies a tenant filter via
`ApplyGranitConventions(currentTenant, dataFilter)`. **However**, that filter
operates on the `IQueryable<T>` LINQ tree — it has no effect on raw SQL passed
through `db.Database.SqlQuery<T>(sql)` or any user-controlled string.

Required guarantees, ranked:

1. **Hard guarantee**: the user cannot bypass `WHERE TenantId = @currentTenant`
   regardless of what they write. Approaches that satisfy this:
   - **Constrained DSL → `IQueryable`** (the global query filter applies)
   - **AST validator that injects the tenant predicate** at the appropriate
     position in the tree (mechanically possible but error-prone — every join,
     every CTE, every sub-query needs the predicate)
   - **PostgreSQL row-level security** with `SET app.current_tenant_id = …`
     per session; `CREATE POLICY tenant_isolation ON meter_events USING
     (tenant_id = current_setting('app.current_tenant_id')::uuid)`. Works at
     the storage engine level, immune to query rewriting. Not portable to
     SQL Server (which requires the security-policy feature, separate setup).

2. **Soft guarantee**: a buggy user query is rejected with a clear error rather
   than silently returning wrong data. Approaches: every option above plus
   defensive `EXPLAIN (FORMAT JSON)` post-parse to verify the planner sees the
   tenant predicate.

**Decision driver**: PostgreSQL RLS is the lowest-risk option *if Granit can
mandate the storage engine*. Granit.Metering currently supports both PG and
SQL Server (#1167); RLS would either fork the implementation or restrict ad-hoc
SQL to the PG provider — acceptable, since this is admin tooling and most
production deployments are PG.

### 3. Resource limits

The default `Granit.Metering` workload runs hourly on commit-sized event
windows (≤ 100 K events for a busy tenant). An ad-hoc query against the same
table without an obvious index access plan can scan the **entire history** of
the `meter_events` table — 100 M rows for a year-old tenant.

| Mechanism | Behavior | Drawback |
| --- | --- | --- |
| `statement_timeout` per session (PG) / `SET LOCK_TIMEOUT` (SQL Server) | DB-side hard cancel | Per-connection setting; the connection pool resets it on return; need a `BeforeOpen` hook to reapply |
| `CommandTimeout` on `DbCommand` | App-side cancel via `CancellationToken` | Cooperative — only checked at row materialisation, not during planner execution |
| Application-level cancellation token wired from HTTP timeout | Cooperative | Same as above |
| Row-count cap via `LIMIT N+1 → reject if N+1 returned` | Truncates results, doesn't bound cost | Doesn't prevent the scan |
| **EXPLAIN before EXECUTE**, reject if `total_cost > X` | Bound BEFORE work starts | PostgreSQL planner cost estimates are coarse; false positives common |
| Read replica with stricter `statement_timeout` and lower memory | Isolates production load | Requires a replica — not in scope for Granit core |

**Decision driver**: combine `statement_timeout` (DB-side hard cancel — this is
the floor) with EXPLAIN-cost gating (rejects obvious foot-guns at meter creation
time). Run all ad-hoc queries through a **dedicated DI-registered
`DbConnection`** that always sets `statement_timeout = 30000` so the limit
cannot accidentally leak into the standard ingestion connection.

### 4. Schema visibility

`MeterEvent` has 9 columns (`Id`, `MeterDefinitionId`, `IdempotencyKey`,
`Quantity`, `Timestamp`, `Metadata`, `TenantId`, `DeprecatedAt`,
`DeprecationReason`). Of these:

- **Always exposed**: `Quantity`, `Timestamp`, `Metadata` — the three columns
  the user actually wants to compute against.
- **Always implicit**: `TenantId` (forced predicate, never selectable),
  `MeterDefinitionId` (joined automatically — the SQL is scoped to one meter).
- **Filtered out**: `DeprecatedAt` (ad-hoc queries see only active events;
  matches the aggregator's behavior — see #1169).
- **Hidden**: `Id`, `IdempotencyKey` — internal plumbing, no billing relevance,
  surfaces a monotonic id that could leak insertion order to other tenants
  via timing.

`Metadata` is JSONB — exposing the JSON path operator (`->>`) is the whole
point of allowing ad-hoc SQL. Whether to expose `->` (returns JSON) and `#>>`
(deep path) is an additional question; the principled answer is "all the
PostgreSQL JSONB path operators, because limiting to `->>'x'` only covers the
flat-key case the existing `CountDistinct.DistinctProperty` already handles".

### 5. Storage

If shipped, the user-defined expression lives on `MeterDefinition` as a new
nullable column:

```csharp
public string? SqlExpression { get; private set; }       // domain
public sealed class MeterDefinitionConfiguration { … }
{
    builder.Property(e => e.SqlExpression).HasMaxLength(4096);
}
```

Auditability:

- **Versioning**: every change goes through `MeterDefinition.Update(...)` which
  is currently restricted to `Draft` status (#1165) — already auditable via
  the audit interceptor.
- **Provenance**: `(CreatedBy, ModifiedBy)` from `AuditedAggregateRoot`
  capture who authored each version.
- **Rollback**: if a SQL expression turns out to compute wrong values, the
  recompute machinery (#1167) handles re-running with the previous expression
  on the affected window — provided the previous expression is recoverable from
  the audit trail.

Versioning the expression itself (à la `PlanPrice` versioning in
`Granit.Subscriptions`) is appealing but doubles the storage model. A simpler
alternative: write each change as an audit log row, never overwrite history.

### 6. Validation pipeline

When the user creates a meter with `SqlExpression`, run synchronously:

1. **Static validation** (microseconds): tokenise + parse, reject on syntax
   errors before the meter is persisted.
2. **AST whitelist** (microseconds): walk the tree, reject any node type not in
   the closed list (DML, DDL, SET, sub-query other than scalar aggregate,
   foreign-table reference, etc.).
3. **EXPLAIN against an empty `meter_events`-shaped temp table** (single-digit
   ms): catches column-name typos, unsupported function calls, broken JSONB
   paths. Cost estimate also bounds the worst-case fan-out.
4. **Smoke execution against the last 5 minutes of events** (50–500 ms):
   verifies the query produces a `numeric`-compatible scalar. The result is
   discarded.

If any step fails, the create returns 422 with a problem-detail mapping the
validation failure to a localised error code (mirrors the `CountDistinct`
validator pattern from #1166).

### 7. Performance

Representative numbers for a busy SaaS tenant on Granit.Metering:

| Scenario | Events / period | SQL aggregate latency (PG, indexed `(TenantId, Timestamp)`) |
| --- | --- | --- |
| Hourly aggregator on rolling window | ≤ 100 K | 50–150 ms per meter |
| On-demand recompute over a day (#1167) | 1–5 M | 300 ms – 2 s per meter |
| Ad-hoc SQL with `metadata->>'x'` filter | 1–10 M | 200 ms – 8 s per meter (varies wildly with selectivity) |
| Ad-hoc SQL with `metadata->>'x'` filter, no index | 10 M | 30 s – timeout |

The pathological case (no functional index on the JSONB path) is the one the
EXPLAIN gate must catch. **Functional indexes on JSONB paths are user-defined
and cannot be inferred from the SqlExpression** — Granit could ship a hint
that any `metadata->>'foo'` predicate triggers a recommendation to
`CREATE INDEX ON meter_events ((metadata->>'foo'));`, but the actual index
creation belongs to the application layer (Granit ships no migrations; #1167).

**Bottom line**: ad-hoc SQL on Granit's PG-on-commodity-hardware target is
viable up to ≈ 10 M events with the right indexes, an order of magnitude
above what the existing primitives need. Beyond that, users should run on a
columnar engine — at which point they likely shouldn't be using Granit anyway.

### 8. Alternatives — constrained DSL

The most attractive alternative to raw SQL is a **constrained predicate +
aggregate DSL** that compiles to `IQueryable<MeterEvent>`. Sketch:

```csharp
public sealed class MeterDefinition
{
    // additive in a future PR if we GO
    public string? PredicateExpression { get; private set; }   // e.g. "metadata.region == 'us-east-1' && metadata.cached == false"
    public string? AggregateExpression { get; private set; }   // e.g. "sum(quantity)" — closed list of fns
}
```

The DSL parser (custom, ~600 LOC for the closed grammar) translates to
`IQueryable<MeterEvent>` predicates and an `Expression<Func<IQueryable<MeterEvent>, decimal>>`
aggregator. EF Core then generates the SQL — and the existing tenant /
soft-delete / `DeprecatedAt == null` filters apply automatically because they
live on the `IQueryable`.

| Property | Raw SQL (whitelisted) | Constrained DSL → IQueryable |
| --- | --- | --- |
| Tenant isolation | Manual (forced WHERE injection) | **Automatic** (global query filter) |
| Soft-delete + lifecycle filters | Manual | **Automatic** |
| Cross-DB portability | Per-provider parser | **Native** (EF Core does it) |
| Expressive power | High (joins, sub-queries, window fns) | Medium (predicate + aggregate; no joins yet) |
| Implementation cost | High (parser + validator + EXPLAIN gate per provider) | Medium (custom DSL parser + Expression-tree builder) |
| Maintenance cost over 5 years | High (track PG / SQL Server syntax churn) | Low (the DSL is a fixed grammar) |
| Failure mode of a parser bug | Tenant data leak | Compile error or wrong number — never a leak |

**This is the recommended GO path**, when GO becomes warranted.

## Recommendation: DEFER

The framework should not ship raw SQL ad-hoc metric definitions today.

| Reason | Weight |
| --- | --- |
| The five built-in aggregation types cover ≥ 95 % of demonstrated B2B SaaS workloads (Granit.Showcase audit + maintainer's own consulting history) | High |
| No concrete user has requested the feature — the demand signal is conjectural ("ORB has it, so we should") rather than empirical ("we lost a deal because of it") | High |
| The safe implementation paths each cost 2–4 weeks of focused work + ongoing maintenance, far above the marginal benefit of the missing 5 % of workloads | High |
| Failure modes are catastrophic — a parser bug or a missed tenant-isolation case directly leaks billing data across tenants (GDPR, ISO 27001 incident-grade severity) | Medium-high |
| Two interim escape hatches exist for users who genuinely need SQL today: pre-compute at ingestion time, or run the query against the application's own DbContext | Medium |

## Path forward (when DEFER expires)

Re-evaluate when **at least three independent users** request the capability for
a scenario the existing five primitives demonstrably cannot serve. At that
point:

1. **Implement the constrained DSL → `IQueryable<MeterEvent>` path** (Question 8
   alternative). 2–3 week effort, zero new attack surface beyond what EF Core
   already protects.
2. Open a tracking issue with three concrete use-cases (each linked to a real
   user / tenant). Without three, the conjectural demand is not enough to
   justify the maintenance burden.
3. Treat raw SQL as a non-goal — even if the DSL turns out to be insufficient,
   the next escalation is to extend the DSL grammar, not to expose raw SQL.
   Raw SQL on a multi-tenant table is a category of feature the framework
   should not own.

## Spike artifacts

- This document.
- No PoC implementation in this iteration. The spike concluded
  **DEFER** before reaching the PoC step (Question 1's analysis was decisive
  enough to halt early). A PoC for the **constrained DSL** path will be built
  the day the GO criteria are met.
- Issue [#1171](https://github.com/granit-fx/granit-dotnet/issues/1171) updated
  with the result.

## See also

- [Phase 2 Story #1166 — `CountDistinct` aggregation](https://github.com/granit-fx/granit-dotnet/issues/1166)
- [Phase 2 Story #1167 — on-demand recompute](https://github.com/granit-fx/granit-dotnet/issues/1167)
- [ADR 032 — Granit.Catalog with Product as the shared billing aggregate](/dotnet/architecture/adr/032-catalog-product-shared-billing-aggregate/)
- [ORB — Metric definitions](https://docs.withorb.com/api-reference/metrics) — external reference
