# Granit.Identity.Federated.Keycloak.BackgroundJobs

Recurring background job that re-runs the Phase 2 Keycloak client-role sync on a
15-minute cadence (`*/15 * * * *`). Additive to the boot-time contributor — both
delegate to the same `KeycloakClientRoleSyncService`, so orphan-policy dispatch
(ADR-029) and idempotent upsert semantics carry over unchanged.

## When to add it

- Deployments where restarts are infrequent and you need drift detection bounded
  by minutes, not weeks.
- Scenarios where Keycloak admins commonly rename / add / delete client roles
  outside a host restart window.

## Wiring

```csharp
services.AddGranitIdentityKeycloak();       // Phase 2 — boot-time sync + provider
// then in the host's modular registration:
services.AddTransient<GranitIdentityFederatedKeycloakBackgroundJobsModule>();
```

`[RecurringJob]` discovery registers the job automatically at startup.
The sync itself is gated by `KeycloakClientRoleSyncOptions.Enabled` — set to
`false` to short-circuit both the boot-time and recurring paths.

## ADR

- ADR-030: scheduled re-sync via `Granit.BackgroundJobs`.
