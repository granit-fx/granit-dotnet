# Granit .NET — Performance & Scalability Checklist

Verification matrix used by the `/perf` skill. Each top-level section maps to a
`--scope` value. Apply in order; lead with the sections that change big-O or
round-trip count (§1, §2, §3), then constant-factor wins.

**Impact legend** — `CRITICAL` (scaling blocker) · `HIGH` (material under load) ·
`MEDIUM` (measurable waste) · `LOW` (minor) · `MICRO-OPT` (only on a proven hot path).

**Provider legend** — 🐘 PostgreSQL (Npgsql, default) · 🟦 SQL Server · 🪶 SQLite.
A tip with no marker holds on all three. PostgreSQL form is given first.

Convention references: `CLAUDE.md`, the Astro docs in the sibling repo
`granit-docs/src/content/docs/dotnet/` (notably `data/persistence.mdx`,
`data/interceptors.mdx`), and the framework primitives named inline.

---

## 1. EF Core & persistence (`--scope efcore`)

The single biggest scaling lever. Most "the app is slow" reports resolve here.

### 1.1 Tracking behavior

- [ ] **Read-only queries use `AsNoTracking()`** (`MEDIUM`→`HIGH` on large sets). Any
  query whose result is projected to a `*Response` and never saved must not pay the
  change-tracker cost (40–60% memory + CPU on big result sets).
- [ ] **Consider a NoTracking default** for read-mostly DbContexts:
  `ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking`, opt into
  `AsTracking()` on write paths. **Caveat for Granit**: write paths then need
  explicit `Update()`/`AsTracking()`; the audit & soft-delete interceptors still run
  on `SaveChanges`, but only for entities EF knows about — verify update flows still
  track. Flag, propose, do **not** auto-apply (semantic change).
- [ ] **`NoTrackingWithIdentityResolution`** where a no-tracking query has duplicated
  related entities and identity matters (dedup without full tracking).
- [ ] No `AsNoTracking()` on a query whose entity is subsequently mutated and saved
  (`CRITICAL` correctness bug — silent no-op save).

### 1.2 Projection — fetch only what you need

- [ ] **Project to `*Response`/anonymous type at the DB level** with `Select(...)`
  (`MEDIUM`→`HIGH`). Pulling whole entities (all columns + unused navigations) to map
  client-side wastes IO, memory, and bandwidth. CLAUDE.md already forbids returning EF
  entities — every read should `Select` the exact columns.
- [ ] No `SELECT *` materialization followed by in-memory `.Select`/`.Where` (client
  evaluation) — push the predicate/shape into the query.
- [ ] Wide tables: never load `text`/`jsonb`/`varbinary` blob columns unless the
  response needs them.

### 1.3 N+1 and round-trips (`CRITICAL` when in a loop)

- [ ] **No query inside a `foreach`/`for`/`Select` over a collection.** Batch with a
  single `Where(x => ids.Contains(x.Id))`. 🐘🟦 EF Core 10 expands `Contains` to
  individual parameters (better plan reuse than the old JSON-array path).
- [ ] **No lazy loading on a hot path** — `Include(...)` eagerly, or project. Detect
  virtual navigations + `UseLazyLoadingProxies`. Lazy load in a loop is the classic N+1.
- [ ] Count via `CountAsync()` / `AnyAsync()` — never `(...).ToList().Count` or
  `.Any()` after materialization.
- [ ] Existence checks use `AnyAsync()`, not `CountAsync() > 0` or `FirstOrDefault() != null`.
- [ ] `FindAsync(id)` (uses the context cache) for by-PK lookups when tracking is on.

### 1.4 Eager-loading shape — cartesian explosion

- [ ] **Multiple collection `Include`s → `AsSplitQuery()`** (`HIGH`). N orders ×
  M items via single query returns N×M rows; split avoids the product. Consider the
  global default `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` and
  override hot, small, well-understood graphs with `AsSingleQuery()`.
- [ ] **Trade-off documented**: split = more round-trips + no single-snapshot
  consistency; single = cartesian blow-up. Choose per query, justify.
- [ ] Filtered includes (`Include(o => o.Items.Where(...))`) instead of loading the
  full collection then filtering in memory.

### 1.5 Pagination — bounded result sets (`CRITICAL` if unbounded)

- [ ] **Every list endpoint paginates with an enforced max page size.** An unbounded
  `ToListAsync()` over a growing table is a scaling time bomb.
- [ ] **Prefer keyset/seek pagination** (`WHERE (created, id) < (@c, @i) ORDER BY
  created DESC, id DESC LIMIT n`) over `Skip(n).Take(m)` on deep pages — `OFFSET`
  scans and discards N rows (degrades linearly with page depth). Use `OFFSET` only for
  shallow, bounded paging. Check `Granit.QueryEngine` for the framework pattern.
- [ ] Ordering is **stable and index-backed** (tie-break on a unique column).

### 1.6 Indexes (emit as guidance — framework ships no migrations, §1.13)

- [ ] **Hot filter/sort/join columns have a covering index.** Cross-check every
  `Where`/`OrderBy`/`Join` column against `HasIndex`. Missing index on a high-cardinality
  filter = seq scan = `HIGH`/`CRITICAL` at scale.
- [ ] **`TenantId` leads composite indexes** in multi-tenant tables — the tenant query
  filter is always applied, so `HasIndex(x => new { x.TenantId, x.OtherCol })` lets the
  planner prune by tenant first.
- [ ] **Enum-as-string columns**: Granit persists enums as PascalCase `varchar`
  (`ApplyGranitConventions`). Indexes on these are string indexes — fine, but ensure
  the column is indexed when filtered, and 🐘 watch collation (use the column's
  default; avoid `LOWER(col)` in `WHERE` which defeats the index — add a functional
  index or store normalized).
- [ ] **Composite index column order** matches the most selective leading predicate
  and the `ORDER BY`.
- [ ] **Filtered/partial indexes** for skewed predicates: 🐘 `HasIndex(...).HasFilter("status <> 'Cancelled'")`
  / partial index; 🟦 filtered index `WHERE`; 🪶 partial index supported. Shrinks the
  index and speeds the common path (e.g. active rows only — pairs with soft-delete).
- [ ] 🐘 **`jsonb` columns queried by content** have a **GIN index** (`USING gin`);
  for key-path lookups consider `jsonb_path_ops`. Plain `json` is not indexable —
  ensure `jsonb`.
- [ ] No redundant/duplicate indexes (write amplification + storage).

### 1.7 Bulk operations vs interceptors (CRITICAL nuance)

- [ ] **`ExecuteUpdateAsync` / `ExecuteDeleteAsync` for set-based mutations** instead
  of load-loop-`SaveChanges` (`HIGH`→`CRITICAL` on large sets — one SQL statement vs N
  tracked entities).
- [ ] **⚠️ They bypass `AuditedEntityInterceptor`, `SoftDeleteInterceptor`, and
  domain-event dispatch.** A bare `ExecuteDeleteAsync` on a soft-deletable entity does
  a **hard delete** and skips the audit trail — a correctness/compliance bug, not an
  optimization. Recommend only where audit/soft-delete/events are provably not
  required (e.g. purging already-soft-deleted rows, see `ISoftDeletePurgeTarget`).
  Otherwise set the soft-delete flag via `ExecuteUpdateAsync` + manual audit columns,
  and **never auto-apply**.
- [ ] **Bulk insert** of many rows: batch `AddRange` + single `SaveChanges` (EF
  batches by default), tuning `MaxBatchSize`. For very high volume consider provider
  bulk copy (🐘 `Npgsql` binary `COPY` / 🟦 `SqlBulkCopy`) — outside EF, document the
  interceptor/event bypass.

### 1.8 Batching & round-trip minimization

- [ ] EF Core batches multiple inserts/updates per `SaveChanges` automatically —
  verify `MaxBatchSize` isn't pinned to 1, and group writes into one `SaveChanges`.
- [ ] Independent read queries on the **same context** are serialized (one DbContext is
  not thread-safe). For true parallel reads use **separate contexts from a factory**
  (`AddDbContextFactory` / pooled) and `Task.WhenAll` — never share a context across
  concurrent awaits (`CRITICAL`: `A second operation was started on this context`).

### 1.9 Compiled & cached queries

- [ ] EF caches the query plan automatically **only when the LINQ tree is stable and
  parameters are parameters**. Flag **dynamically-constructed queries that inline
  constants** (e.g. building `Where` with a captured literal via expression trees /
  string interpolation in `FromSql`) — each variant misses the plan cache and re-compiles
  (`HIGH` under load). Pass values as parameters.
- [ ] **`EF.CompileAsyncQuery`** for genuinely hot paths (>~1k executions/min, stable
  shape) to skip the cache lookup + expression compilation. Measure first — propose,
  don't auto-apply.
- [ ] `FromSql`/`SqlQuery` use **interpolated/parameterized** form (`FromSql($"... {id}")`
  → parameter), never string concatenation (plan-cache miss **and** SQL injection).

### 1.10 Provider configuration & resiliency

- [ ] 🐘 PostgreSQL uses **`UseGranitNpgsql`** (already sets `EnableRetryOnFailure(3,
  30s)` plus `CommandTimeout(30)`). Don't re-add; tune per workload (long reports may
  need a higher command timeout via a dedicated context).
- [ ] 🟦 SQL Server context enables `EnableRetryOnFailure` (SqlServerRetryingExecutionStrategy).
- [ ] 🪶 SQLite: **single-writer** — serialize writes, keep transactions short, enable
  WAL (`PRAGMA journal_mode=WAL`) for concurrent readers; expect no server-side
  retry strategy. Treat SQLite as dev/edge/test, not high-concurrency prod.
- [ ] **`ExecutionStrategy` + manual transactions**: when retry is on, user
  transactions must run **inside** `Database.CreateExecutionStrategy().ExecuteAsync(...)`
  or they throw. Flag manual `BeginTransaction` not wrapped in the strategy.
- [ ] **Connection string pool sizing** tuned for load: `Maximum Pool Size` ×
  replica count must stay under the server's `max_connections` (🐘 default 100). 🐘
  high-replica deployments should sit behind **PgBouncer** (transaction pooling) —
  then disable prepared-statement persistence (`No Reset On Close` / `Max Auto Prepare=0`)
  to stay compatible.
- [ ] Command timeout set per workload; long analytics queries isolated from the
  request-path context.

### 1.11 Modeling for performance

- [ ] **Inheritance mapping**: prefer **TPH** (single table + discriminator) for read
  performance — TPT/TPC add joins/unions. Flag TPT on a hot aggregate.
- [ ] **Owned types / `jsonb` columns** (🐘 `ToJson()`) for value-shape sub-objects
  read/written together — fewer tables/joins than separate entities. Don't over-use:
  not queryable by inner fields without a GIN index.
- [ ] **Value converters cost**: heavy converters (encryption, JSON) run per row and
  block server-side translation of the converted column (no index seek on the plaintext).
  Granit's `SingleValueObject` converters are cheap; flag expensive custom converters
  on filtered columns. (`Granit.Encryption` columns are intentionally opaque — don't
  filter on them.)
- [ ] **Denormalize / store-computed** for expensive aggregates read far more than
  written (counts, rollups) — maintain via domain events rather than computing on every
  read. Architectural; propose with the event wiring.
- [ ] Concurrency: **`IConcurrencyAware`** (ADR-061) is the framework optimistic-
  concurrency primitive (xmin 🐘 / rowversion 🟦). Use it instead of pessimistic locks
  for contended aggregates; reserve `pg_advisory_xact_lock` (🐘, see `GranitDbProviders`)
  for genuine pessimistic needs (e.g. `EntityMergeConcurrencyLock`).

### 1.12 Testing & measurement

- [ ] **Never benchmark/perf-test on the EF in-memory provider** — it skips query
  translation and round-trips, hiding the real cost. Use **Testcontainers** with the
  real provider (PostgreSQL first), per CLAUDE.md testing conventions.
- [ ] Enable SQL logging in a perf investigation (`LogTo`, `EnableSensitiveDataLogging`
  in dev only) and read the generated SQL; 🐘 `EXPLAIN (ANALYZE, BUFFERS)` / 🟦 `SET
  STATISTICS IO ON` to confirm index usage. Never leave sensitive-data logging on in prod.

### 1.13 Migrations & schema lifecycle

- [ ] **Framework packages ship NO migrations** — index/schema recommendations are
  emitted as guidance + sample SQL / public `*ModelBuilderExtensions` for the consuming
  app to own (CLAUDE.md, project memory).
- [ ] **No `Database.Migrate()` at app startup in multi-replica deployments** — racing
  replicas corrupt the schema. Use a dedicated migration job/init-container
  (`Granit.Persistence.EntityFrameworkCore.Hosting` / `GranitMigrationRunner`) that
  runs once and exits, gated before app rollout. (`CRITICAL` scaling/microservice hazard.)

---

## 2. Allocations & GC (`--scope allocations`)

Reduce per-request garbage → fewer Gen-0/2 collections → lower tail latency under load.
**Only pursue below CRITICAL/HIGH on paths the profiler proved hot (§9).**

- [ ] **LINQ in proven hot paths** allocates enumerators/closures/delegates per call —
  replace with `for`/`foreach` over the concrete collection or a `Span<T>` loop
  (`MICRO-OPT` unless hot). Do **not** delinearize cold code.
- [ ] **String building**: no `+=`/`String.Concat`/`String.Format` in loops → reuse a
  `StringBuilder`, interpolation (single shot), or `string.Create`/`ISpanFormattable`.
- [ ] **`[LoggerMessage]` everywhere** (already a Granit rule) — kills boxing +
  interpolation allocs in logging and skips formatting when the level is disabled.
- [ ] **`[GeneratedRegex]`** for every regex (compile once, no per-call allocation).
- [ ] **`ArrayPool<T>` / `MemoryPool<T>`** for transient large buffers (file/stream/
  serialization scratch) instead of `new byte[]` per request — pairs with the
  buffer-leak pattern in dumps (§9).
- [ ] **`Span<T>`/`ReadOnlySpan<T>`/`stackalloc`** for slicing/parsing without copies;
  CLAUDE.md already favors `params ReadOnlySpan<T>`.
- [ ] **Object pooling** (`ObjectPool<T>`) for expensive-to-create, reusable objects
  (e.g. `StringBuilder`, scratch buffers) on hot paths.
- [ ] **Avoid boxing**: generic constraints over `object` params; no value type →
  `object`/interface in hot loops; `EqualityComparer<T>.Default` over boxing equals.
- [ ] **Stream, don't buffer**: return `IAsyncEnumerable<T>` / write to the response
  stream for large result sets instead of materializing a giant `List` (caps memory,
  starts the response sooner). Pair with EF `AsAsyncEnumerable()`.
- [ ] **Large Object Heap**: buffers/arrays > 85 KB go to the LOH (compacted rarely) —
  pool or chunk them; flag per-request large allocations.
- [ ] **Exceptions are not control flow** — throwing is expensive and allocates; high
  `exception-count` (§9) means refactor to `Try*`/result types on the hot path.
- [ ] **Capacity hints**: pre-size `List<T>`/`Dictionary<,>`/`StringBuilder` when the
  count is known, to avoid resize-copy churn.
- [ ] **.NET 10 wins are free** — the framework targets .NET 10; ensure projects don't
  pin down-level langversion/runtime that forfeits JIT/GC/BCL improvements (e.g.
  improved `LINQ`, `Span`, JSON, and GC throughput). Verify `<ServerGarbageCollection>`
  and tiered-PGO settings (§7) so the runtime gains actually apply.

---

## 3. Async & concurrency (`--scope async`)

Wrong async = threadpool starvation = the whole service stalls under load (`CRITICAL`).

- [ ] **No sync-over-async**: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`,
  `Task.Run(...).Result` on request/handler paths block a pool thread → starvation.
  `await` all the way (`CRITICAL` under load). (Already a CLAUDE.md anti-pattern.)
- [ ] **`ConfigureAwait(false)` in library code** (Granit packages are libraries) —
  already a convention; verify on every `await` in non-ASP.NET library code.
- [ ] **`CancellationToken` flows end-to-end** and is honored (last param, passed to
  every async call incl. EF `*Async` and HTTP). Unhonored cancellation wastes work
  under load/timeouts.
- [ ] **`Task.WhenAll` for independent awaits** instead of sequential `await`s — but
  **never** parallelize over a single shared `DbContext` (§1.8); use separate
  contexts/clients.
- [ ] **`ValueTask`/`ValueTask<T>`** for hot async methods that frequently complete
  synchronously (cache hits) — fewer `Task` allocations. Don't await a `ValueTask`
  twice.
- [ ] **`async void`** only for event handlers — everywhere else `Task` (unobserved
  exceptions + uncatchable).
- [ ] **No blocking on locks across `await`**: `lock`/`Monitor` can't span awaits — use
  `SemaphoreSlim.WaitAsync`. Prefer `System.Threading.Lock` (CLAUDE.md) for short
  sync-only critical sections.
- [ ] **Concurrent collections** (`ConcurrentDictionary`, `Channel<T>`) over `lock` +
  `Dictionary` on contended shared state; producer/consumer via `System.Threading.Channels`.
- [ ] **`IAsyncEnumerable` with `[EnumeratorCancellation]`** for async streams; consume
  with `await foreach`.
- [ ] **No fire-and-forget** unawaited tasks that swallow exceptions or outlive the
  request scope (esp. capturing scoped services / `DbContext` → leak/disposed-context).
- [ ] **Min threadpool / starvation**: if profiling shows a growing queue (§9), the fix
  is removing blocking calls, not raising `ThreadPool.SetMinThreads` (band-aid).

---

## 4. Caching — `Granit.Caching` (`--scope caching`)

Caching is a primary scaling lever — a cached hot read removes a DB round-trip on
every request × every replica. Granit ships an opinionated stack; **use it correctly,
do not hand-roll**. Know the two APIs and what the framework already does for you.

### 4.0 The two cache APIs — pick the right one

- **`IFusionCache`** (ZiggyCreatures FusionCache, registered by `AddGranitCaching()`)
  is **THE cache-aside API**. Use `GetOrSetAsync(key, factory, options, tags, ct)` for
  every read-through cache. Do **not** reach for `IConditionalCache` or a raw
  `IMemoryCache`/`IDistributedCache` for standard caching (`MEDIUM`→`HIGH` finding:
  re-implementing what FusionCache gives for free).
- **`IConditionalCache`** is **only** for atomic conditional writes —
  `SetIfAbsentAsync` (SET NX), `SetIfPresentAsync` (SET XX), `GetAsync`, `DeleteAsync`
  — i.e. distributed locks, idempotency state machines, compare-and-swap. Flag it used
  as a plain cache (wrong tool) **and** flag a hand-rolled lock where `SetIfAbsentAsync`
  would do.

### 4.1 What the framework already provides — do NOT re-implement (and do NOT disable)

`AddGranitCaching()` + `FusionCachingOptions` (`"Cache:FusionCache"`) ship
production-grade defaults. Re-doing these manually, or overriding them off per call, is
the finding:

- [ ] **Stampede protection is automatic** — FusionCache does per-key factory locking
  (single-flight), so a stampede on a cold hot key rebuilds **once**, not N×. Flag any
  hand-rolled `GetOrAdd`/`lock`+`Dictionary`/double-checked cache (`HIGH`: reinventing
  it, usually without the lock) → replace with `GetOrSetAsync`.
- [ ] **Fail-safe is on by default** (`FailSafeIsEnabled=true`, max 2 h, throttle 30 s)
  — stale entries are served when the factory fails, shielding the DB during an
  outage/spike. Flag per-call `FusionCacheEntryOptions` that set `IsFailSafeEnabled=false`
  on a hot key without justification.
- [ ] **Soft/hard factory timeouts** (2 s soft / 10 s hard) return stale instead of
  hanging on a slow backend. Don't remove them; tune per workload if needed.
- [ ] **Eager refresh at 80 % of TTL** (`EagerRefreshThreshold=0.8`) refreshes in the
  background before expiry so requests rarely hit a cold miss. Keep it on for hot keys
  (set to 0 only intentionally).
- [ ] **Default duration**: absolute 1 h (`Cache:DefaultAbsoluteExpirationRelativeToNow`)
  wired into `FusionCacheEntryOptions.Duration`. FusionCache has **no sliding
  expiration** (by design — it doesn't compose with L2/backplane); do not assume or
  recommend sliding behavior. Set an explicit `Duration` per entry when the data's
  volatility differs — don't leave volatile data on the 1 h default (staleness) or
  pin rarely-changing data to a short TTL (needless misses).

### 4.2 Coverage — what should be cached

- [ ] **Hot, rarely-changing reads go through `IFusionCache`** (`HIGH` win): permission
  & query/export definitions, feature flags, settings, tenant metadata, OIDC discovery
  docs, localization, BFF/session lookups. Each uncached one is a DB round-trip per
  request × replicas. Identify them via §9 metrics (low/no hit traffic on a hot read).
- [ ] **Don't cache volatile or per-request-unique data** — near-zero hit ratio just
  adds serialization + memory cost. Cache effectiveness is measured (4.6), not assumed.

### 4.3 Multi-replica: L1 + L2 + backplane

- [ ] **L2 Redis + backplane wired when scaling out** (`CRITICAL` at scale): default is
  L1 in-memory only. Add **`Granit.Caching.StackExchangeRedis`** — it upgrades
  FusionCache with `WithRegisteredDistributedCache()` + a Redis pub/sub backplane
  (channel prefix `granit:fc`) so an eviction on one pod invalidates L1 on all pods.
  Pure per-replica in-memory cache across R replicas = R× the misses **and** stale
  divergence after a write on one pod.
- [ ] **`IConditionalCache` is in-memory by default** — it only becomes cluster-wide
  (Redis SET NX/XX) with `Granit.Caching.StackExchangeRedis`. An in-memory
  `IConditionalCache` used as a *distributed* lock across replicas is a correctness bug
  (`CRITICAL`) — confirm the Redis impl is registered before relying on it for
  cross-pod mutual exclusion / idempotency.

### 4.4 Tenant isolation — automatic; don't fight it

- [ ] **Keys are tenant-prefixed automatically.** The injected `IFusionCache` is the
  `TenantAwareFusionCache` decorator: it prefixes keys/tags with `t:{tenantId:N}:` (or
  `t:host:`). So **do NOT manually concatenate the tenant id into the key** — that
  double-prefixes and fragments the cache. Flag manual tenant prefixing on `IFusionCache`.
- [ ] **`IConditionalCache` is NOT tenant-decorated** — for per-tenant locks/idempotency
  you **must** include the tenant id in the key yourself. Flag a per-tenant
  `SetIfAbsentAsync` with a bare global key (`CRITICAL` cross-tenant collision).
- [ ] **The raw (non-decorated) cache** (`TenantAwareFusionCache.RawCacheKey`,
  `"__granit_raw_cache__"`) bypasses tenant prefixing. Using it for per-tenant data is a
  `CRITICAL` isolation leak — only legitimate for genuinely global/host data; justify it.

### 4.5 Invalidation, sizing, encryption

- [ ] **Tag-based invalidation**: pass `tags:` to `GetOrSetAsync` and evict groups with
  `RemoveByTagAsync` instead of tracking every key. Use it for "invalidate everything
  for entity X / tenant T" (tags are tenant-prefixed too).
- [ ] **Invalidation is event-driven for must-be-fresh data** — wire domain events to
  `RemoveAsync`/`RemoveByTagAsync`, don't rely on TTL alone. Document the consistency
  window for TTL-only data.
- [ ] **Bounded L1 size + eviction** — an unbounded in-memory cache grows into a
  Gen-2/LOH leak (shows in dumps, §9). Confirm size limits on high-cardinality keyspaces.
- [ ] **Encrypt sensitive cached values** (`CRITICAL`, GDPR/ISO 27001): set global
  `Cache:EncryptValues=true` or mark the type `[CacheEncrypted]` (per-type override wins;
  `[CacheEncrypted(false)]` opts out). AES-256 key from **Vault/ESO only**
  (`Cache:Encryption:Key`), never committed. Note: encryption protects the **L2**
  (serialized Redis) payload — L1 holds live objects in-process. Never cache secrets in
  plaintext L2. Cross-check `/security`.

### 4.6 Measure cache effectiveness (ties to §9)

- [ ] **Watch the `Granit.Caching` meter**: `granit.caching.entry.hit` /
  `.entry.miss` (hit ratio — a hot key with a low ratio is mis-tuned TTL or wrong key
  shape), `granit.caching.fail_safe.activated` (rising = the backend is failing, cache
  is masking it — investigate the source), `granit.caching.factory.timeout` (slow
  factory — the cached read itself is slow, fix §1/§5). All tagged `tenant_id`.
- [ ] **A new cache must move the hit-ratio / DB-round-trip metric** — verify with §9,
  don't assume. Caching the wrong thing adds cost with no benefit.

### 4.7 HTTP-level caching (complements value caching)

- [ ] **HTTP output caching** (`Granit.Http.OutputCaching`, + `.StackExchangeRedis`
  variant for multi-replica) for anonymous/cacheable GETs — skips the whole pipeline,
  not just the DB. Vary by tenant/auth correctly (an auth-varying response cached
  globally is a `CRITICAL` leak — cross-check `/security`).
- [ ] **Conditional requests**: `ETag`/`Last-Modified` + `If-None-Match`/`If-Modified-Since`
  to return `304` and skip body serialization + transfer on unchanged resources.

---

## 5. HTTP, serialization & API (`--scope http`)

- [ ] **System.Text.Json source-gen** (`JsonSerializerContext`) for hot DTOs — avoids
  reflection-based metadata per serialize (`HIGH` on chatty APIs); aligns with
  Granit's native OpenAPI 3.1 stance (no Swashbuckle/NSwag).
- [ ] **`IHttpClientFactory`** always (CLAUDE.md anti-pattern: `new HttpClient()` →
  socket exhaustion). Typed/named clients with pooled handlers; wire
  `Granit.Http.Resilience` (Polly) for retries/circuit-breakers, not hand-rolled.
- [ ] **Response compression** (`Granit.Http.ResponseCompression`) for large
  text/JSON responses (Brotli/Gzip) — big bandwidth + latency win; ensure it's not
  applied to already-compressed payloads and is safe vs auth (BREACH — cross-check
  `/security`).
- [ ] **Pagination cap on every list endpoint** (`CRITICAL` if unbounded — mirrors §1.5)
  with a hard server-side max page size, regardless of client request.
- [ ] **Stream large payloads** (file download/export) via `FileStreamHttpResult` /
  `IAsyncEnumerable`, not a buffered `byte[]`/`List` (caps memory, TTFB). Pairs with
  `Granit.DataExchange` exports.
- [ ] **Rate limiting** (`Granit.Http.RateLimiting`) + **bulkhead/concurrency limits**
  (`Granit.Http.Bulkhead`) on expensive endpoints to protect the service and DB pool
  under burst — a scaling safety valve, not just abuse prevention.
- [ ] **Idempotency** (`Granit.Http.Idempotency`) on unsafe retried operations — avoids
  duplicate work/writes under client retries.
- [ ] **Avoid over-fetching / chatty round-trips** — collapse N small calls; use
  `Granit.Http.ODataExposure`/`QueryEngine` for client-shaped queries instead of
  bespoke endpoints that over-return.
- [ ] **Minimal API** (already the Granit standard) over MVC controllers — lower
  per-request overhead. Verify endpoint filters (validation, etc.) aren't doing heavy
  per-request work that could be cached.
- [ ] **OpenAPI/metadata generation** is build/startup-time, not per-request — confirm
  no schema generation on the request path.

---

## 6. Messaging & Wolverine (`--scope messaging`)

Granit uses WolverineFx (v6, runtime compilation pkg required — see project memory)
with a durable outbox/inbox on 🐘 `Granit.Wolverine.Postgresql` / 🟦
`Granit.Wolverine.SqlServer`.

- [ ] **Distributed events (`*Eto`) go through the outbox** (transactional consistency,
  at-least-once) — already a convention; flag any `IDistributedEventBus` publish that
  bypasses it. Local `*Event` via `ILocalEventBus` stays in-process (no round trip) —
  flag `*Eto` used where a local event suffices (wasted bus + DB write).
- [ ] **Handler cost**: handlers are on the message-throughput path — same EF rules
  (§1) apply. No N+1, no sync-over-async, project not load. A slow handler caps queue
  drain rate.
- [ ] **Idempotent handlers** (`CRITICAL` multi-replica): at-least-once delivery + N
  replicas means a handler can run twice/concurrently. Use the inbox dedup + design
  for re-execution; flag handlers with non-idempotent side effects (double charge,
  double email).
- [ ] **Batching / parallelism**: tune Wolverine listener parallelism and batch size to
  the workload; ensure ordered processing only where required (ordering throttles
  throughput).
- [ ] **Outbox/inbox table growth**: confirm a cleanup/retention job exists — an
  ever-growing outbox table degrades every publish (`HIGH` over time). Indexes on the
  outbox status/time columns.
- [ ] **Poison-message handling**: dead-letter + bounded retry so a bad message doesn't
  spin a handler hot (CPU burn + queue stall).
- [ ] **No per-message DbContext leak / no shared context across the handler chain** —
  scoped per message.

---

## 7. Startup & runtime configuration (`--scope startup`)

- [ ] **Server GC** enabled for server workloads: `<ServerGarbageCollection>true</ServerGarbageCollection>`
  (+ usually `<ConcurrentGarbageCollection>true`). Workstation GC on a multi-core
  server throttles throughput (`HIGH`). Set on the **host app** (csproj/runtimeconfig),
  not library packages.
- [ ] **Tiered compilation + PGO** on (default in .NET 10): `<TieredPGO>true`. Verify
  nothing disables tiered compilation (hurts steady-state throughput). For latency-
  sensitive startup, consider `<TieredCompilationQuickJit>` tuning + **ReadyToRun**
  (`<PublishReadyToRun>true`) for the host.
- [ ] **DbContext pooling** (`AddDbContextPool` / `AddPooledDbContextFactory`) for
  high-throughput services — reuses context instances, cutting per-request allocation
  and reset cost. **Caveat**: pooled contexts must not capture per-request state in
  fields; Granit's `GranitDbContext` injects `ICurrentTenant`/`IDataFilter` — confirm
  pooling is compatible (tenant resolved per-request via the scoped accessor, not
  cached in the context). Propose, verify, don't auto-apply.
- [ ] **Connection pool sizing** coherent with replica count (§1.10) — Min/Max pool,
  and DB `max_connections` headroom.
- [ ] **Lazy / deferred init**: heavy singletons built at first use, not blocking
  startup; no synchronous IO in `ConfigureServices`/module init.
- [ ] **Health checks are cheap** (CLAUDE.md: 10s timeout, readiness vs liveness split)
  — a liveness probe that hits the DB couples probe load to DB and can cascade restarts
  under load.
- [ ] **Native AOT** is generally **not** a fit for this reflection/EF-heavy framework —
  don't recommend it broadly; ReadyToRun is the pragmatic startup win.
- [ ] **Compiled model** (`dotnet ef dbcontext optimize`) for very large models to cut
  first-query/startup model-build time — app-owned, propose for big consumers.

---

## 8. Horizontal scaling & multi-tenancy (`--scope scaling`)

Reason at "N tenants × M rows × R replicas × C concurrent". Overlaps `/audit
--scope microservices`.

- [ ] **Stateless request handling** — no per-request state in singletons / static
  fields; session/state in distributed store (Redis), not process memory. (`CRITICAL`
  for horizontal scale.)
- [ ] **In-memory cache is not a source of truth** across replicas — use L2/Redis
  backplane (§4) or accept per-replica drift only for derivable data.
- [ ] **Distributed locks** where cross-replica mutual exclusion is needed (🐘
  `pg_advisory_lock`, Redis lock) — never an in-process `lock` for cluster-wide
  coordination.
- [ ] **Recurring/background jobs run once cluster-wide**, not per replica
  (`CRITICAL`): a `[RecurringJob]` firing on every replica multiplies load N×. Confirm
  the scheduler (`Granit.Scheduling`/`Granit.BackgroundJobs`) dedups across replicas.
- [ ] **Idempotent handlers/jobs** (mirror §6) — re-execution safe.
- [ ] **No migrations at boot** (§1.13) — dedicated migration job.
- [ ] **Tenant fan-out cost**: operations iterating all tenants (broadcast jobs,
  per-tenant rebuilds) must batch/throttle and not hold a connection per tenant — flag
  O(tenants) connection or round-trip patterns.
- [ ] **Tenant filter parameterization** (§2e in SKILL) — `GranitDbContext` keeps the
  tenant id a SQL parameter so the plan cache isn't fragmented per tenant and the value
  can't leak across requests. Flag any re-inlining.
- [ ] **Sharding / schema-per-tenant** (🐘 `SET search_path`, `GranitDbDefaults.HostDbSchema`):
  if used, confirm connection reuse doesn't leak `search_path` across pooled connections
  (reset on return) and that the schema switch is parameter-safe.
- [ ] **Backpressure**: bulkhead/rate-limit (§5) + bounded queues so one hot tenant
  can't starve the cluster.

---

## 9. Observability & profiling (`--scope observability`)

The measurement layer that makes every other section evidence-based.

- [ ] **Module metrics exist and are useful**: `IMeterFactory` meters
  (`granit.{module}.{entity}.{action}`) + `ActivitySource` per module (CLAUDE.md). For
  perf, confirm latency/throughput/error counters on the hot paths and `tenant_id` tag
  for per-tenant breakdown. Use these as the first signal before attaching a profiler.
- [ ] **Cache effectiveness** via the `Granit.Caching` meter —
  `granit.caching.entry.hit`/`.miss` (hit ratio), `granit.caching.fail_safe.activated`,
  `granit.caching.factory.timeout`, tagged `tenant_id`. Low hit ratio on a hot key,
  rising fail-safe, or factory timeouts each point back to §4 / §1. (Detailed in §4.6.)
- [ ] **OpenTelemetry** traces span DB/HTTP/message boundaries so slow spans are
  attributable (Granit ships OTel 1.15+). Verify EF + HttpClient + Wolverine
  instrumentation is enabled in the host.
- [ ] **The profiling workflow** (see SKILL "Live profiling mode") is available:
  `dotnet-counters` for runtime health, `dotnet-trace` for CPU hotspots,
  `dotnet-gcdump`/`dotnet-dump` for memory/leaks. Baseline normal load, then capture
  the problematic state, diff.
- [ ] **BenchmarkDotNet** (`[MemoryDiagnoser]`, `Baseline = true`) for any proposed
  hot-path micro-optimization — against a **real provider via Testcontainers** for EF
  paths (§1.12). A change without a before/after table is a hypothesis, not a win.
- [ ] **Logging volume**: high-throughput paths don't log at `Information` per request
  (IO + allocation); `[LoggerMessage]` with appropriate levels, sampling for hot paths.
- [ ] **No PII in perf logs/traces/metrics** (GDPR, cross-check `/security`).
- [ ] **Artifacts cleaned up** after every investigation (`*.nettrace`, `*.gcdump`,
  `*.dmp`, coverage XML) — confirm `git status` clean.

---

## Quick triage — where to look first by symptom

| Symptom | Start at scope | Likely cause |
| --------- | --------------- | -------------- |
| Endpoint slow, DB CPU high | `efcore` | N+1 (§1.3), missing index (§1.6), no projection (§1.2) |
| Endpoint slow, app CPU high | `allocations` / `http` | LINQ/string churn (§2), reflection JSON (§5) |
| Memory grows unbounded | `allocations` / `caching` | buffer/cache leak (§2, §4.5), tracked entities (§1.1) |
| Threadpool queue climbs, throughput collapses | `async` | sync-over-async (§3) |
| Fine on 1 replica, breaks on N | `scaling` / `caching` | per-replica jobs (§8), in-memory cache (§4.3) |
| Slow only on deep pages | `efcore` | `OFFSET` pagination (§1.5) |
| Fast on PostgreSQL, slow on SQL Server/SQLite | `efcore` (`--provider`) | provider divergence (§1.10/§1.11) |
| Latency spikes / GC pauses | `allocations` / `startup` | Gen-2 pressure (§2), Workstation GC (§7) |
| Message queue drains slowly | `messaging` | slow handler, no batching (§6) |
