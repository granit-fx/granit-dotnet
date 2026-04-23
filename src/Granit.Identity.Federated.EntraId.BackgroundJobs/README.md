# Granit.Identity.Federated.EntraId.BackgroundJobs

Recurring background job that re-runs the Phase 2 Entra ID App Role sync on a
15-minute cadence (`*/15 * * * *`). Additive to the boot-time contributor — both
delegate to the same `EntraIdClientRoleSyncService`, so orphan-policy dispatch
(ADR-029) and idempotent upsert semantics carry over unchanged.

## When to add it

- Deployments where the Azure portal is the source of truth for App Roles and
  admin edits happen without a host restart.
- Multi-region deployments where Graph-side mutations should propagate to every
  replica within ~15 minutes rather than on the next rolling deploy.

## Wiring

```csharp
services.AddGranitIdentityEntraId();       // Phase 2 — boot-time sync + provider
// then in the host's modular registration:
services.AddTransient<GranitIdentityFederatedEntraIdBackgroundJobsModule>();
```

`[RecurringJob]` discovery registers the job automatically at startup.
The sync itself is gated by `EntraIdClientRoleSyncOptions.Enabled` — set to
`false` to short-circuit both the boot-time and recurring paths.

## ADR

- ADR-030: scheduled re-sync via `Granit.BackgroundJobs`.
