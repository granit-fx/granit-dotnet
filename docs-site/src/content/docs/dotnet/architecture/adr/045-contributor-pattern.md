---
title: "ADR-045: Inversion-of-control contributor pattern"
description: "Three Granit primitives — workspaces, entity relations (smart buttons), activity types — share a single Inversion-of-Control contribution pattern. A module declares a target by name; other modules graft items onto that target via a typed IContributor interface, registered in DI, resolved at boot. Zero hard dependency between extender and target. Missing target = silent drop. Conflict order = explicit Order property + alphabetical fallback. Same shape as IConfigureOptions<T>."
sidebar:
  order: 45
  label: "045 - IoC Contributor Pattern"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Workspaces.Abstractions`, `Granit.Entities.Abstractions`, `Granit.Activities.Abstractions`)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1524](https://github.com/granit-fx/granit-dotnet/issues/1524) — ADR IoC contributor pattern
> **Status:** Accepted

## Context

The Refonte UI Hybride initiative ([#1506](https://github.com/granit-fx/granit-dotnet/issues/1506)) introduces three primitives that all face the same extensibility problem:

- **Workspaces** ([ADR-044](./044-workspace-navigation)): a workspace shell (e.g., `Granit.Framework.Automation`) is declared in one module; other modules (`Granit.BackgroundJobs.Endpoints`, `Granit.Webhooks.Endpoints`) need to graft items into its sections without the shell knowing about them.
- **Entity relations** (smart buttons, ADR-048): an entity (`Party` in `Granit.Parties`) gets cross-module smart buttons grafted from `Granit.Invoicing`, `Granit.Subscriptions`, `Granit.Payments`, `Granit.CustomerBalance` — none of those modules can be hard-coded into `Granit.Parties`.
- **Activity types** (ADR-046): the framework declares the `Activity` polymorphic aggregate; modules add domain-specific types (`Quote` from `Granit.Sales`, `Onboarding` from `Granit.Identity`) without coupling.

The naive solutions all fail in known ways:

- **Hard reference from owner to extender** (`Granit.Parties` references `Granit.Invoicing`) → cyclic dependencies; the lower-tier module knows about higher-tier ones; build graph inverts.
- **Mega-module that declares everything** (`Granit.AllSmartButtons` references every business module) → couples release cadence; pulling one feature pulls them all; unworkable at scale.
- **Convention-based assembly scanning** (Frappe's app `hooks.py` discovers events) → magic, fragile, slow boot; misnamed conventions silently fail.
- **XML inheritance with XPath patches** (Odoo) → brittle anchors, unpredictable order across module install combinations, runtime view-merger cost — all documented pain points (PR #1599 conversation).

ASP.NET Core has the answer staring at us: `IConfigureOptions<TOptions>` is the canonical IoC pattern for "anyone can contribute to T." Granit standardises on the same shape, with a typed contributor interface per primitive.

## Decision

### 1. One pattern, three interfaces

Each extensible primitive ships its own typed contributor interface in its `*.Abstractions` package:

| Primitive | Contributor interface | Where it lives | Targets |
| --------- | --------------------- | -------------- | ------- |
| Workspace nav | `IWorkspaceContributor` | `Granit.Workspaces.Abstractions` | A workspace name (`"Granit.Framework.Automation"`) |
| Entity relations | `IEntityRelationContributor` | `Granit.Entities.Abstractions` | A target entity name (`"Granit.Parties.Party"`) |
| Activity types | `IActivityTypeProvider` | `Granit.Activities.Abstractions` | The framework's activity registry |

All three follow the same **Contribute(context)** shape. The implementation differs (because the things being contributed differ), but the registration, resolution, and conflict-handling rules are identical.

### 2. Canonical contract — workspace example

```csharp
// In Granit.Workspaces.Abstractions
public interface IWorkspaceContributor
{
    int Order => 0;                            // ordering within a target

    void Contribute(WorkspaceContributionContext ctx);
}

public sealed class WorkspaceContributionContext
{
    public IWorkspaceContributionTarget For(string workspaceName);
}

public interface IWorkspaceContributionTarget
{
    IWorkspaceContributionTarget Section(string key, Action<SectionBuilder> configure);
}

// Registration extension
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceContribution<T>(this IServiceCollection services)
        where T : class, IWorkspaceContributor =>
        services.AddSingleton<IWorkspaceContributor, T>();
}
```

```csharp
// In Granit.BackgroundJobs.Endpoints
public sealed class BackgroundJobsWorkspaceContribution : IWorkspaceContributor
{
    public int Order => 100;

    public void Contribute(WorkspaceContributionContext ctx) =>
        ctx.For("Granit.Framework.Automation").Section("background-jobs", s => s
            .Entity<BackgroundJob>("default")
            .Entity<BackgroundJobExecution>("default")
            .Dashboard<BackgroundJobsHealthDashboard>());
}

// Module DI
public override void ConfigureServices(ServiceConfigurationContext ctx)
{
    ctx.Services.AddBackgroundJobsEndpoints();
    ctx.Services.AddWorkspaceContribution<BackgroundJobsWorkspaceContribution>();
}
```

The same shape applies to `IEntityRelationContributor` (registers `services.AddEntityRelationContribution<T>()`) and `IActivityTypeProvider` (registers `services.AddActivityTypeProvider<T>()`).

### 3. Resolution rules — identical across the three

At boot (or first request — implementation detail per primitive), the framework:

1. Loads all `IWorkspaceContributor` (or relation/activity contributors) from DI.
2. Calls `Contribute(ctx)` on each.
3. Each `ctx.For(targetName)` lookup:
   - **Target exists** → grafted items merge into the target.
   - **Target missing** → **silent drop** with a debug log (`Workspace 'Granit.Framework.Automation' not found; contribution from BackgroundJobsWorkspaceContribution dropped`).
4. Within a target, contributions are **ordered by `Order`** ascending; ties broken alphabetically by contributor type name.
5. Empty target after all contributions = invisible (workspace not exposed in `GET /api/workspaces`; entity relation absent from manifest; activity type unavailable to the type registry).

### 4. Soft dependency — the Abstractions package guarantee

The contributor interfaces live in `*.Abstractions` packages (lightweight, no runtime). A consuming module pulls only the abstraction:

```xml
<ProjectReference Include="..\Granit.Workspaces.Abstractions\Granit.Workspaces.Abstractions.csproj" />
```

If the application doesn't pull the runtime (`Granit.Workspaces`), the contribution is registered in DI but never consumed — zero observable effect. This is the **soft-dependency guarantee**: a module that contributes UI nav doesn't force the entire app to expose a workspace UI.

The `AbstractionsPurityTests` architecture test (introduced in #1603, extended in #1605) enforces that `*.Abstractions` packages stay runtime-free. The IoC contract relies on this purity.

### 5. Conflict resolution — explicit Order, deterministic fallback

When multiple modules contribute to the same target, the order matters (which smart button appears first on the Party detail; which dashboard appears first in the Automation workspace).

Two-tier resolution:

- **`Order` property** (default `0`) — explicit hint. Lower values render first.
- **Alphabetical fallback** by contributor type name — deterministic when `Order` ties.

Avoid relying on `Order` for security or correctness; it's a UX-ordering hint, not a dependency declaration. If two modules need contributions in a specific order for the system to work, that's a design smell — make one module depend on the other's data shape directly via a typed contract.

### 6. Boot-time integrity check (configurable)

The framework ships an opt-in integrity check that fails boot when any `EntityDefinition` declares a reference (`b.Workflow<X>()`, `b.Dashboard<Y>()`) to a primitive that isn't registered in DI. Default mode is `Throw`; `Warn` and `Off` are configurable per environment:

```csharp
services.AddGranitEntities(opt => opt.IntegrityCheck = IntegrityCheckMode.Throw);
```

For contributors specifically, the integrity check verifies each `ctx.For(targetName)` resolves at least one target across the loaded module set. A typo in the target name (`"Granit.Framework.Automatation"`) fails boot in `Throw` mode rather than silently dropping.

### 7. Out of scope — no scripted contributions

Granit's IoC pattern accepts only **typed C# implementations** of the contributor interface. There is no analog of Frappe's `hooks.py` (Python dictionary registering callbacks) or Odoo's `ir.actions.server` (Python in DB). Both are documented liabilities (PR #1599 conversation). Contributions are compiled, audited, refactor-friendly.

## Consequences

### Positive

- One pattern to learn — apply it three times. The cognitive load is amortised across workspaces, relations, and activity types; future primitives that need extensibility can adopt the same shape with no new convention.
- Zero hard dependency between contributor and target. `Granit.Invoicing` can graft a smart button onto `Granit.Parties.Party` without `Granit.Parties` knowing `Granit.Invoicing` exists.
- Soft-dependency guarantee via `*.Abstractions` packages — modules that contribute don't force runtime adoption.
- Same shape as `IConfigureOptions<T>` — familiar to any ASP.NET Core developer; idiomatic Granit.
- Conflict resolution is deterministic (Order + alphabetical) — no install-order surprises across deployments.
- Boot-time integrity check catches typos before they become silent drops in production.

### Negative / accepted trade-offs

- The number of small contributor classes can grow large (one per cross-module relation, one per workspace section per module). This is the cost of IoC's decoupling. Naming convention: `<Source>On<Target>RelationContribution.cs` for relations, `<ModuleName>WorkspaceContribution.cs` for workspaces.
- Tenant admins (Tier B) cannot author contributors. Customizations live at the workspace-layout level (Phase 2) or as custom items in the workspace UI; new compiled contributors are a Tier A dev concern.
- Silent-drop default for missing targets means a misspelled target name doesn't fail in dev — until the integrity check kicks in. Apps that want fail-fast should set `IntegrityCheck = Throw` (the recommended default).

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. Contributors are Tier A (compiled); they cannot be authored by tenant admins.
- [ADR-044](./044-workspace-navigation) — Workspace navigation. The first consumer of `IWorkspaceContributor`; documents the section/item shape that contributions populate.
- [ADR-046](./046-activities-vs-timeline) — Activities vs Timeline. The `IActivityTypeProvider` consumer.
- [ADR-048](./048-cross-module-entity-relations) — Cross-module entity relations. The `IEntityRelationContributor` consumer.
- [PR #1603](https://github.com/granit-fx/granit-dotnet/pull/1603) — `Granit.Workflow.Abstractions` split + `AbstractionsPurityTests` infrastructure. The architecture test that guarantees the soft-dependency property holds for every `*.Abstractions` package the IoC contract relies on.

## References

- ASP.NET Core `IConfigureOptions<TOptions>` — the canonical IoC contribution pattern in the .NET ecosystem. Granit's contributors mirror its registration shape (`services.AddOptions<T>().Configure<...>()` ↔ `services.AddWorkspaceContribution<T>()`).
- Frappe `hooks.py` and `doctype_js` — research compiled in PR #1599 conversation. Convention-based, untyped, fragile. Granit's deliberate inversion: typed interface, DI-registered, integrity-checked.
- Odoo XML view inheritance with XPath — research compiled in PR #1599 conversation. Brittle anchors, install-order dependent. Granit replaces it with the typed contribution shape (per [ADR-044](./044-workspace-navigation) §1).
- Microsoft.Extensions.DependencyInjection — `IEnumerable<IService>` resolution as the foundation: every `services.AddSingleton<IWorkspaceContributor, T>()` adds to the same enumerable; the framework iterates at consume time. No magic.
