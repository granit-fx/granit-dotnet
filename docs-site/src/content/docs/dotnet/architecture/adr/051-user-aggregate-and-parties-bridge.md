---
title: "ADR-051: User aggregate in Granit.Identity + optional Parties bridge"
description: "Granit consolidates LocalIdentity and FederatedIdentity behind a canonical User aggregate that lives in Granit.Identity (foundation). When the optional Granit.Parties.Identity bridge is loaded, every UserCreated emits a Party of kind Person via Wolverine; the bridge is opt-in, so tiny apps that only need authentication keep zero Parties dependency."
sidebar:
  order: 51
  label: "051 - User aggregate"
---

> **Date:** 2026-05-01
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Identity` (NEW User aggregate root), `Granit.Identity.Local`, `Granit.Identity.Federated`, `Granit.Authorization`, `Granit.Parties.Identity` (NEW bridge module — see [Amendment 1](#amendment-1--bridge-module-naming-pivot-2026-05-02)).
> **Epic:** [#1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence (parent of the OData / Entities follow-ups that motivated this).
> **Status:** Accepted (amended 2026-05-02 — see [Amendments](#amendments))
> **Implementation:** Shipped across PRs #1708 → #1717, #1719 between 2026-05-01 and 2026-05-02.

## Context

Today Granit models *"a person who can authenticate"* as two parallel hierarchies:

- **`GranitUser`** in `Granit.Identity.Local` — extends `IdentityUser<Guid>`. Carries 25+ properties: profile (`UserName`, `Email`, `FirstName`, `LastName`, `PhoneNumber`), auth secrets (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`), security counters (`LockoutEnd`, `AccessFailedCount`, `TwoFactorEnabled`).
- **`UserCacheEntry`** in `Granit.Identity.Federated` — cache projection of users authenticated against an external IdP (Cognito / Entra ID / Google / Keycloak). Holds `ExternalUserId`, encrypted profile fields, `LastSyncedAt`, `MetadataJson` for provider-specific claims.

`IIdentityUser` (per [ADR-019](./019-user-lookup-dual-mode-cache-vs-local-store)) is implemented by both, but the abstraction is **lookup-only** — `IUserLookupService.FindByIdAsync` and friends — not `IQueryable`. One implementation is active per host (`AspNetIdentityUserLookupService` for local, `CachedUserLookupService` for federated); hybrid is not supported.

**Three intertwined problems surface:**

1. **No unified read view for admin / BI.** The OData feed needs `IQueryable<IIdentityUser>` to compose `$filter` / `$select`. The admin grid today only sees local users; federated users are invisible from the admin "Users" collection.
2. **Type inconsistency on user FKs.** `Party.UserId: Guid?`, `SubscriptionSeat.UserId: Guid`, `GranitUserGroupMember.UserId: Guid` are hardcoded `Guid` — they only work for local users. In federated mode the IdP's user id is a string (`sub`).
3. **No deduplication concept.** A physical person could simultaneously have a `GranitUser` row AND a `UserCacheEntry` (e.g. local admin who later signs in via Cognito). The framework has no notion of *"these two rows are the same person"*.

Phase 2 of `Granit.Entities` (ADR-050) made `EntityDefinition` the gate for OData EntitySet exposure, surfacing problem (1) bluntly: `GranitUser` cannot ship on the BI feed because it has no `EntityDefinition`, and even if it did, the EntitySet would only show locals.

This ADR locks the canonical-user shape going forward.

## Decision drivers

- **Modular framework.** Granit is a framework, not an ERP. Tiny apps that need only `Granit.Identity.Local` (no parties, no business records) must keep working without taking on the `Granit.Parties` dependency.
- **Odoo's `_inherits` pattern is the reference but not the constraint.** Odoo says every `res.users` MUST have a `res.partner`. That fits an ERP with mandatory `res.partner`. Granit's modularity precludes "Parties is always loaded".
- **`Granit.Identity` is the foundation module.** Every Granit app loads it (auth is non-negotiable). It's the right home for the canonical user concept.
- **Pre-1.0 freedom.** No data migration constraints — seed data can be regenerated. Breaking changes are clean (no `[Obsolete]` graduation per the framework's pre-1.0 rule).

## Considered alternatives

The exploration considered four directions before locking the chosen one. Captured here so the rationale is visible:

### A — Unified read view at the OData / admin layer only

`IUserDirectoryQueryableSource` returning `IQueryable<IIdentityUserDescriptor>`. Local impl projects from `GranitUser`; federated impl projects from `UserCacheEntry`. Composite impl for hybrid hosts.

- **Pros**: no schema change, solves problem (1) cleanly.
- **Cons**: write side stays bifurcated, problem (2) untouched, problem (3) untouched.
- **Rejected** as the primary solution but kept as a *property* of the chosen design (the User aggregate provides `IQueryable<User>` natively).

### B — `Person` aggregate in a new `Granit.People` module

New top-level aggregate. Existing entities (Local, Federated, Party) all become children pointing at `PersonId`.

- **Pros**: cleanest modeling, solves all three problems.
- **Cons**: massive refactor (5–10 PRs across modules), adds a new module dependency for every app.
- **Rejected** — over-engineered relative to the actual problem and the solo-maintainer cost.

### B' — Party-as-Person (Odoo-style, `Party` is canonical)

Make `Party` the canonical anchor with kind=Person. `LocalIdentityLink` and `FederatedIdentityLink` are children of `Party`.

- **Pros**: reuses existing aggregate, Odoo-validated pattern.
- **Cons**: forces `Granit.Parties` dependency on every Granit app — kills modularity for tiny apps. Requires inversion of dependency direction (Identity → Parties or Parties → Identity).
- **Rejected** because of the "tiny app without Parties" use case JF flagged.

### B'' — User canonical in Granit.Identity, with optional Parties bridge (CHOSEN)

User is its own aggregate root in `Granit.Identity` (foundation). `LocalIdentity` and `FederatedIdentity` are siblings that reference `User.Id`. A separate optional module `Granit.Identity.Parties` syncs Party records when both Identity and Parties are loaded.

- **Pros**: tiny apps work without Parties; Identity stays self-contained; Authorization's User-Role-Group references stay inside Identity (no cross-module lookups); migration scope is bounded.
- **Cons**: bridge handlers must be designed carefully (see "Edge cases" below); existing `GranitUser` and `UserCacheEntry` get renamed.
- **Chosen**.

## Decision

```text
Granit.Identity (foundation, NEW aggregate)
  └── User
        ├── Id: Guid
        ├── DisplayName, Email, FirstName, LastName, PhoneNumber
        ├── PreferredLocale, IsEnabled
        ├── TenantId (IMultiTenant)
        ├── LocalIdentityId: Guid?           ← FK 0..1
        └── (FederatedIdentities resolved via FK from FederatedIdentity table)

Granit.Identity.Local
  └── LocalIdentity (renamed from GranitUser)
        ├── Id: Guid
        ├── UserId: Guid (FK → User)
        ├── extends IdentityUser<Guid>
        └── PasswordHash, SecurityStamp, LockoutEnd, EmailConfirmed, …

Granit.Identity.Federated
  └── FederatedIdentity (renamed from UserCacheEntry)
        ├── Id: Guid
        ├── UserId: Guid (FK → User)
        └── Provider, ExternalSubject, LastSyncedAt, MetadataJson

Granit.Authorization
  └── Role / RoleMetadata / PermissionGrant / GranitUserGroupMember
        → all reference User.Id (Guid) directly, never LocalIdentity.Id

Granit.Identity.Parties (NEW bridge, OPTIONAL)
  ├── On UserCreatedEto       → CreatePartyForUserHandler   (always create new Party)
  ├── On UserProfileChangedEto → SyncProfileToPartyHandler   (Name / Email primary)
  └── On PartyChanged         → no reverse sync by default
```

### Locked design choices

| Decision | Value | Rationale |
| -------- | ----- | --------- |
| Canonical aggregate | `User` in `Granit.Identity` | Foundation module owns the table; every Granit app already loads it. |
| `LocalIdentity` / `FederatedIdentity` shape | **Referenced** (separate aggregates with FK to `User.Id`), not owned | ASP.NET Identity's `UserManager<TUser>` works most cleanly when `TUser` is the top-level aggregate. Owned children would force `UserManager<User>` to project through. Referenced keeps the existing flow. |
| Bridge emission pattern | Wolverine **integration events (`*Eto`)** | Outbox-based, eventual consistency, cross-DbContext (Identity and Parties live in different DbContexts). |
| Email uniqueness | **None** — Odoo-style. Multiple Parties may share an email. | Odoo's default — JF tested empirically. Same email can legitimately exist on multiple Parties (employee + customer + contact at the same company). |
| Bridge complexity | 3 handlers: `EnsurePartyForUser`, `SyncProfileToParty`, no reverse sync by default | Email-match heuristics rejected — always create a new Party on user creation. |
| Bridge granularity | Optional module `Granit.Identity.Parties`; tiny apps work without it | The very point of the modular architecture. |
| Migration of existing data | **Not needed** — pre-1.0, regenerate Showcase seed | No production data to preserve. |

## Bridge contract

When a host loads `Granit.Identity.Parties`, three Wolverine handlers wire up:

### Handler 1 — `EnsurePartyForUser` on `UserCreatedEto`

```csharp
On UserCreatedEto event →
  Always create a NEW Party
    ├── Kind = Person
    ├── Name = User.DisplayName
    ├── Emails = [{ Address = User.Email, IsPrimary = true }]
    ├── (FirstName, LastName) = (User.FirstName, User.LastName)
    └── PartyIdentityLink: PartyId → User.Id
```

No find-by-email matching. No deduplication attempt. The handler always creates a fresh Party — Odoo's `_inherits` pattern. Multiple Parties sharing an email is a feature, not a bug.

### Handler 2 — `SyncProfileToParty` on `UserProfileChangedEto`

```csharp
On UserProfileChangedEto event →
  If Party linked via PartyIdentityLink exists →
    Update Party.Name / FirstName / LastName / Emails primary
```

One-way sync only: User → Party. The Party's other emails (secondary, archived) are untouched. The `IsPrimary` flag may move within `Party.Emails` if the User's email changes to one already present on the Party (rare).

### Handler 3 — No reverse sync by default

Changes on `Party.Emails.Primary` do NOT propagate back to `User.Email`. This is deliberate:

- The User's email is the auth-recovery target. Auto-syncing from Party would let a Party admin redirect password resets — security-relevant.
- The Party email may legitimately differ from the auth email (Party = business contact, User = login).

If a host needs the reverse direction, they wire their own handler explicitly. The bridge does not ship one.

## Edge cases

### 1. User created without a Party link

Acceptable. The User exists; the bridge just doesn't link it. An admin can manually link via the Party admin UI later. Zero data integrity issue — the link is tracked by `PartyIdentityLink`, which can be `null`.

### 2. GDPR — User deletion

When a User is erased (per `Granit.Privacy`), the linked Party survives. Rationale: the Party may have business records attached (Invoices, Subscriptions); deleting it would orphan those. The Party becomes "auth-less" — its `PartyIdentityLink` is removed. The Party's profile data follows the regular pseudonymisation flow.

### 3. GDPR — Party deletion

When a Party is erased, the linked User survives. Rationale: the User is an Identity-side concept independent of the Party's business role. The Party's deletion just severs the link.

This **asymmetry is deliberate** — User and Party are independent aggregates, each with its own erasure policy. The bridge only knows how to *create* and *update*; deletion of either side is owned by the source module.

## Migration

**No data migration in this iteration.** Granit is pre-1.0; the Showcase seed regenerates. Real-world hosts that already have `GranitUser` / `UserCacheEntry` tables would need a one-shot migration — out of scope for this ADR; will be its own story when the first production host adopts.

## Implementation plan

Tracked as a multi-PR EPIC, each step shippable independently:

1. **B-step 1** — `Granit.Identity` introduces the User aggregate (table, `UserQueryDefinition`, `UserExportDefinition`, `UserEntityDefinition`, `IUserDirectoryQueryableSource`, `UserCreatedEto` / `UserProfileChangedEto`).
2. **B-step 2** — `Granit.Identity.Local` rename `GranitUser` → `LocalIdentity`, add `LocalIdentity.UserId`, update ASP.NET Identity wiring.
3. **B-step 3** — `Granit.Identity.Federated` rename `UserCacheEntry` → `FederatedIdentity`, add `FederatedIdentity.UserId`. Update Phase 2's `UserCacheEntryEntityDefinition` to `FederatedIdentityEntityDefinition`.
4. **B-step 4** — `Granit.Authorization` repointing — Role / Group / RoleMetadata / PermissionGrant references migrate from local-style FK to direct FK at `User.Id`.
5. **B-step 5** — `Granit.Identity.Parties` new bridge module: `PartyIdentityLink` entity + 3 Wolverine handlers + DI extension.
6. **B-step 6** (optional) — FK alignment cleanup on `Subscriptions.Subscription.UserId`, `Party.UserId`.

Each PR ships with: build clean, format clean, shard tests pass, integration tests of the new aggregate, Showcase seed regenerated and verified.

## Amendments

The original decision text above is preserved as written on 2026-05-01.
The following amendments record changes that emerged during
implementation between 2026-05-01 and 2026-05-02.

### Amendment 1 — Bridge module naming pivot (2026-05-02)

**Original:** Bridge module was locked as `Granit.Identity.Parties` —
the "extension *Parties* of Identity" reading.

**Amended:** The bridge ships as **`Granit.Parties.Identity`** instead.

**Why:** The repo already has a precedent for this kind of bridge —
[`Granit.Parties.MultiTenancy`](https://github.com/granit-fx/granit-dotnet/tree/develop/src/Granit.Parties.MultiTenancy)
ships the handler that creates a host-scoped `Party` on
`TenantCreatedEvent`. The convention is "the bridge lives in the
**consumer's** tier, named after what it consumes" — Parties is the
consumer (it does the Party creation), Identity / MultiTenancy are
the events it subscribes to. Naming the new bridge
`Granit.Parties.Identity` keeps the two bridge modules visually and
hierarchically grouped under the `Granit.Parties.*` prefix — which
matters more for the `*.Notifications` / `*.Privacy` pattern this
codebase has been consistent about.

The contract (3 handlers, opt-in module, no reverse sync) is
unchanged — only the package name moved.

**Where to adjust your reading:**

- Every reference to `Granit.Identity.Parties` in the body above
  should be read as `Granit.Parties.Identity`.
- The diagram in the [Decision](#decision) section keeps the old name
  for historical accuracy.

### Amendment 2 — Implementation expanded with auto-create steps (2026-05-02)

**Original plan:** Six B-steps (1 → 6), with B-step 1 emitting
`UserCreatedEto` / `UserProfileChangedEto` directly.

**Amended:** Five intermediate `*.5` steps were inserted to keep each
PR mechanical and reviewable. The runtime auto-create plumbing
(joint hydration of `User` alongside `LocalIdentity` /
`FederatedIdentity`) and the event emission turned out to be
load-bearing for the bridge and large enough to deserve their own
focused PRs.

The shipped sequence:

| Step | PR | Title |
| ---- | -- | ----- |
| B-0 | [#1708](https://github.com/granit-fx/granit-dotnet/pull/1708) | ADR-051 |
| B-1 | [#1709](https://github.com/granit-fx/granit-dotnet/pull/1709) | `User` aggregate primitive |
| **B-1.5** | [#1710](https://github.com/granit-fx/granit-dotnet/pull/1710) | EF Core companion (`IdentityDbContext`, `IUserDirectoryQueryableSource`) |
| B-2 | [#1711](https://github.com/granit-fx/granit-dotnet/pull/1711) | `GranitUser` → `LocalIdentity` rename + `UserId` FK |
| **B-2.5** | [#1712](https://github.com/granit-fx/granit-dotnet/pull/1712) | `LocalIdentity` runtime auto-create (`IUserDirectoryWriter` + `LocalIdentityManager.CreateAsync` override) |
| B-3 | [#1713](https://github.com/granit-fx/granit-dotnet/pull/1713) | `UserCacheEntry` → `FederatedIdentity` rename + `UserId` FK |
| **B-3.5** | [#1714](https://github.com/granit-fx/granit-dotnet/pull/1714) | `FederatedIdentity` runtime auto-create (joint hydration in `CachedUserLookupService`) |
| B-4 | [#1715](https://github.com/granit-fx/granit-dotnet/pull/1715) | Authorization repointing (doc-only — alignment guarantee already had the right Guid) |
| **B-4.5** | [#1716](https://github.com/granit-fx/granit-dotnet/pull/1716) | `User` aggregate emits `UserCreatedEto` / `UserProfileChangedEto` (pre-req for the bridge subscriber) |
| B-5 | [#1717](https://github.com/granit-fx/granit-dotnet/pull/1717) | `Granit.Parties.Identity` bridge — `EnsurePartyForUserHandler` + `SyncProfileToPartyHandler` |
| B-6 | [#1719](https://github.com/granit-fx/granit-dotnet/pull/1719) | `UserId` doc cleanup (`SubscriptionSeat`, `AIUsageRecord`, `Party`) |

Why this matters retrospectively:

- **`B-1.5`** — The plan had EF Core wiring as part of B-1. Splitting
  it kept B-1 a pure abstractions PR and B-1.5 a focused EF
  companion, both reviewable on their own.
- **`B-2.5` / `B-3.5`** — Runtime auto-create was implicit in the
  rename steps (B-2 / B-3) but is the load-bearing piece that makes
  the rename usable. Splitting them kept the rename PRs mechanical.
- **`B-4.5`** — The plan assumed `UserCreatedEto` / `UserProfileChangedEto`
  shipped in B-1. They didn't (the User aggregate had no event
  raising in B-1), so a separate step inserted the event records and
  wired them into `User.Create` / `User.UpdateProfile` before B-5
  could subscribe.

### Amendment 3 — `PartyIdentityLink` entity dropped (2026-05-02)

**Original B-step 5:** Bridge module ships a `PartyIdentityLink`
entity (or just leverages `Party.UserId` if we keep it as the sync
target — TBD in ADR).

**Amended:** No `PartyIdentityLink` entity. The bridge writes
directly to the existing `Party.UserId` Guid column.

**Why:** `Party.UserId` already exists and already enforces the
"one user → at most one Party" invariant via a partial unique
index. A separate join entity would duplicate that with no benefit
— the link is intrinsically 1:1 between the two aggregates. Dropping
it kept the bridge module to ~150 lines.

## Cross-references

- [ADR-019](./019-user-lookup-dual-mode-cache-vs-local-store) — User Lookup dual mode. This ADR builds on the `IIdentityUser` contract introduced there; the new `User` aggregate is the canonical concrete implementation.
- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. The User aggregate ships with its own `EntityDefinition` (Tier A).
- [ADR-050](./050-odata-edm-whitelist-via-entity-definition) — OData EDM whitelist via EntityDefinition. The User aggregate becomes OData-eligible once B-step 1 lands.

## References

- Odoo `res.users._inherits = {'res.partner': 'partner_id'}` — delegation inheritance pattern. Validated empirically that Odoo creates a fresh Partner per User without any email-uniqueness constraint.
- Granit.Entities Phase 1 cobayes (`PartyEntityDefinition`, `InvoiceEntityDefinition` — #1564, #1565) — canonical shape mimicked by the new `UserEntityDefinition`.
