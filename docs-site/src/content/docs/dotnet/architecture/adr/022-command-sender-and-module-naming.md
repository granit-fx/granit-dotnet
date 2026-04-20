---
title: "ADR-022: ICommandSender and Module Naming — No Technology Suffix on Domain Modules"
description: "Command dispatch is abstracted behind Granit.Commands.ICommandSender in the core Granit assembly. Business modules drop the .Wolverine suffix and rapatriate their handlers; adapter modules that carry genuine Wolverine-specific infrastructure keep the suffix; integration adapters get a descriptive suffix (.Privacy, .Provisioning) instead."
sidebar:
  order: 22
  label: "022 - ICommandSender + Module Naming"
---

> **Date:** 2026-04-20
> **Authors:** Jean-Francois Meyers
> **Scope:** `granit-dotnet` — all business modules (`Granit.Payments`, `Granit.Invoicing`, `Granit.Subscriptions`, `Granit.CustomerBalance`, `Granit.Auditing.Privacy`, `Granit.Notifications.Privacy`, `Granit.Identity.Federated.Privacy`, `Granit.Identity.Local.Privacy`, `Granit.MultiTenancy.Provisioning`), plus `Granit.Commands`, `Granit.Wolverine`, `Granit.Invoicing.Abstractions`.

## Context

Fourteen business modules shipped a companion `Granit.{Module}.Wolverine` adapter
package whose only role was to host Wolverine message handlers and a thin dispatcher
wrapping `IMessageBus`. A typical adapter contained:

```csharp
// Granit.Payments.Wolverine / Internal / WolverinePaymentCommandDispatcher.cs
internal sealed class WolverinePaymentCommandDispatcher(IMessageBus messageBus) : IPaymentCommandDispatcher
{
    public Task SendAsync(InitiatePaymentCommand command, CancellationToken ct = default)
        => messageBus.SendAsync(command);
}
```

and a handler:

```csharp
// Granit.Payments.Wolverine / Handlers / InitiatePaymentCommandHandler.cs
public sealed partial class InitiatePaymentCommandHandler
{
    public static async Task HandleAsync(InitiatePaymentCommand command, /* services */, CancellationToken ct)
    { /* domain logic, no Wolverine type used */ }
}
```

Analysis across the 14 adapters revealed three distinct categories:

1. **Cosmetic coupling (11 modules)** — Handlers used the Wolverine discovery
   convention (`public class` + `public static HandleAsync`) but imported **zero**
   Wolverine types. The dispatcher was a 7-line façade. The `.Wolverine` suffix
   exposed an implementation detail in a public package name. Swapping Wolverine for
   another bus would force a breaking rename of consumer-visible packages.

2. **Genuine Wolverine infrastructure (3 modules)** — `Granit.Scheduling.Wolverine`,
   `Granit.Webhooks.Wolverine`, `Granit.Notifications.Wolverine` use `DeliveryOptions`,
   custom headers tied to middleware, local queue routing, or retry policies whose
   semantics are part of the Wolverine pipeline.

3. **Integration adapters, miscategorized (5 modules)** — `Granit.Auditing.Wolverine`,
   `Granit.Notifications.Privacy.Wolverine`, `Granit.Identity.Federated.Wolverine`,
   `Granit.Identity.Local.Wolverine`, `Granit.MultiTenancy.Wolverine`. These ship
   privacy providers or tenant provisioning logic — the "Wolverine" in the name
   refers only to the handler convention, not to the purpose of the adapter.

Additionally, five consumers across business modules (`Granit.Payments.Endpoints`,
`Granit.Invoicing.BackgroundJobs`, `Granit.Metering.BackgroundJobs`,
`Granit.Subscriptions.BackgroundJobs`) injected `Wolverine.IMessageBus` directly to
dispatch commands or publish events — silently leaking the implementation detail
into the domain layer.

## Decision

### 1. `ICommandSender` — provider-agnostic command dispatch contract

`Granit.Commands.ICommandSender` is introduced in the core `Granit` assembly, next
to the existing `Granit.Events.ILocalEventBus` / `IDistributedEventBus`:

```csharp
namespace Granit.Commands;

public interface ICommandSender
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class;
}
```

The implementation lives in `Granit.Wolverine` and delegates to `IMessageBus`. The
name `ICommandSender` (rather than `ICommandBus`) avoids collision with
`Wolverine.ICommandBus`.

Business modules inject `ICommandSender` instead of `Wolverine.IMessageBus`.
Per-module dispatcher interfaces (`IPaymentCommandDispatcher`,
`IInvoiceCommandPublisher`, etc.) are removed — they were domain-specific aliases of
`ICommandSender` with no added value.

For events, the existing `IDistributedEventBus` / `ILocalEventBus` remain the
canonical abstractions.

### 2. Module naming — no technology suffix on domain modules

Three tiers, each with a clear naming rule:

| Tier | Naming rule | Examples |
| ---- | ----------- | -------- |
| **Core domain** | `Granit.{Module}` — no suffix | `Granit.Payments`, `Granit.Invoicing`, `Granit.Subscriptions`, `Granit.CustomerBalance` |
| **Integration adapter** | `Granit.{Module}.{Integration}` — suffix describes *what*, not *with what* | `Granit.Auditing.Privacy`, `Granit.Notifications.Privacy`, `Granit.MultiTenancy.Provisioning` |
| **Infrastructure adapter** | `Granit.{Module}.Wolverine` — suffix legitimately ties the module to a single bus implementation | `Granit.Scheduling.Wolverine`, `Granit.Webhooks.Wolverine`, `Granit.Notifications.Wolverine` |

Handlers move out of the `.Wolverine` adapter into the base domain module
(`Granit.{Module}/Handlers/`). Wolverine discovers them via `opts.Discovery.IncludeAssembly(...)`
across all loaded `GranitModule` assemblies — unchanged behavior.

### 3. `IInvoiceCreditApplier` — thin integration primitive

To let `Granit.CustomerBalance` rapatriate its `CustomerBalancePrePaymentProcessor`
without pulling the full `Granit.Invoicing` package, a thin integration interface
is added in `Granit.Invoicing.Abstractions`:

```csharp
public interface IInvoiceCreditApplier
{
    Task ApplyCreditAsync(Guid invoiceId, decimal amount, CancellationToken ct = default);
}
```

The default implementation in `Granit.Invoicing` hides `IInvoiceReader`/`IInvoiceWriter`
and `Invoice.ApplyCreditNote()`. `Granit.CustomerBalance` now depends only on
`Granit.Invoicing.Abstractions` (contracts) rather than the full module.

### 4. Architecture test — `WolverineCouplingTests`

Two guard-rail tests in `Granit.ArchitectureTests` enforce the separation at build time:

1. `Domain_packages_should_not_use_Wolverine_namespace` — scans every `.cs` file
   under `src/` and fails if a non-allowlisted project contains `using Wolverine;`.
2. `Domain_packages_should_not_reference_WolverineFx_package` — scans every csproj
   and fails if a non-allowlisted project references the `WolverineFx` NuGet package.

The allowlist enumerates the core Wolverine infrastructure, the three legitimate
`.Wolverine` adapters (Scheduling, Webhooks, Notifications), and two cross-cutting
exceptions (`Granit.Privacy` — defines Wolverine Sagas; `Granit.Scheduling.BackgroundJobs`
— uses `DeliveryOptions` for catch-up).

### 5. Dispatch abstractions — `ICommandSender` vs `IBackgroundJobDispatcher` vs event buses

Under Wolverine, all three abstractions eventually call `IMessageBus.SendAsync` — they
overlap technically on single-handler one-off dispatch. The framework keeps them
separate because the chosen injection type documents the **intent**, not the
implementation. A reader opening a class can tell from the constructor parameter alone
whether the code belongs to the domain flow, a pub-sub integration, or a system
work unit.

| Abstraction | Intent | Shape | Examples |
| ----------- | ------ | ----- | -------- |
| `Granit.Commands.ICommandSender` | CQRS command — single handler, domain intent | Strongly typed: `SendAsync<TCommand>(TCommand)` | `InitiatePaymentCommand`, `CreateInvoiceCommand`, `ExecuteExportCommand` |
| `Granit.BackgroundJobs.Abstractions.IBackgroundJobDispatcher` | Operational / system work — one-off, scheduled, or recurring | Weakly typed: `PublishAsync(object, headers)` + `ScheduleAsync` + `[RecurringJob]` | `OrphanBlobCleanupJob`, `QuotaThresholdScanJob` |
| `Granit.Events.ILocalEventBus` / `IDistributedEventBus` | Pub-sub fan-out — zero-or-more subscribers | Strongly typed: `PublishAsync<TEvent>(TEvent)` | `InvoiceFinalizedEto`, `PaymentFailedEto`, `TrialExpiringEvent` |

Consolidation (folding `ICommandSender` into `IBackgroundJobDispatcher`) was considered
and rejected: the weak typing of `PublishAsync(object)` degrades the command API, and
recurring/scheduled dispatch is irrelevant to CQRS commands. The cost of two thin
abstractions wrapping the same bus is lower than the cost of losing the intent signal
in consumer code.

### 6. Collapse dispatch-only adapters into the base module

`Granit.DataExchange.Wolverine` and `Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine`
existed only to replace an in-memory Channel-based dispatcher with an `IMessageBus`-backed
one. Both adapters are deleted: the per-module dispatcher interfaces
(`IExportCommandDispatcher`, `IImportCommandDispatcher`, `IMigrationBatchDispatcher`) and
their Channel + Worker fallbacks are removed from the base modules. Consumers inject
`ICommandSender` directly; the existing handlers live in `Granit.{Module}/Handlers/`.
This removes redundant abstractions (both layers were wrapping `IMessageBus.SendAsync`)
and makes the messaging provider dependency explicit — a host that uses DataExchange or
Migrations must register an `ICommandSender` implementation, which `Granit.Wolverine`
supplies by default per ADR-005.

## Consequences

### Benefits

- **Public package names describe the domain, not the infrastructure.** A consumer
  who installs `Granit.Payments` from nuget.org cannot tell which bus implementation
  the framework chose — that's an implementation detail.
- **Zero transitive `WolverineFx` dependency** on core domain packages. Consumers
  get Wolverine only by opting into `Granit.Wolverine`.
- **Migrating to a different bus (MassTransit, NServiceBus, Rebus)** is a
  replacement of `Granit.Wolverine` + the three infrastructure adapters. No public
  package of any business module changes name.
- **Integration intent is visible in the name.** `Granit.Auditing.Privacy` tells
  a developer it integrates the audit trail with the Privacy export pipeline;
  `Granit.Auditing.Wolverine` said nothing meaningful.
- **Guard-rail prevents regression.** The architecture test catches silent
  reintroduction of `using Wolverine;` in a domain module.

### Trade-offs

- **Breaking change on public API.** Per-module dispatcher interfaces
  (`IPaymentCommandDispatcher`, etc.) are removed. Downstream consumers migrate to
  `ICommandSender`. For a solo maintainer with internal consumers, the cost is
  local; external consumers need a migration note in release notes.
- **Rename churn on 5 adapter modules.** Historical git links, NuGet references,
  and external documentation that cite `Granit.Auditing.Wolverine` etc. must be
  updated. Mitigated by `git mv` preserving file history.
- **New `IInvoiceCreditApplier` primitive.** Adds one interface and one service in
  the Invoicing contract surface. The benefit (decoupled modules, testable in
  isolation, reusable for future credit-like features — gift cards, refund
  surpluses) outweighs the single extra file.

### Evaluated alternatives

1. **Keep `.Wolverine` adapters, only rename dispatchers.** Rejected — leaves the
   cosmetic suffix on public packages.
2. **Rapatriate everything into base modules, accept `WolverineFx` dep.** Rejected
   — force-feeds `WolverineFx` to consumers who never opt into Wolverine.
3. **Replace `.Wolverine` suffix on domain modules with `.Messaging`.** Rejected —
   `Granit.Payments.Messaging` is semantically empty (messaging is an
   implementation concern, not a domain one); the base module is the natural home.
4. **Introduce `IMessagingTopologyBuilder` in core.** Considered for Notifications
   and Webhooks (queue routing, retry policies). Deferred — the `.Wolverine` suffix
   on those two is honest about the coupling, and a generic topology abstraction
   would be premature without a second bus implementation to generalize from.

## References

- [Granit.Commands.ICommandSender](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit/Commands/ICommandSender.cs)
- [Granit.Wolverine.Internal.WolverineCommandSender](https://github.com/granit-fx/granit-dotnet/blob/develop/src/Granit.Wolverine/Internal/WolverineCommandSender.cs)
- [Granit.ArchitectureTests.WolverineCouplingTests](https://github.com/granit-fx/granit-dotnet/blob/develop/tests/Granit.ArchitectureTests/WolverineCouplingTests.cs)
- [ADR-005: Wolverine & Cronos](../005-wolverine-cronos/) — the original choice of Wolverine as the messaging implementation
- [ADR-020: Declarative Definitions Placement](../020-declarative-definitions-placement/) — same placement principle applied to query/export definitions
