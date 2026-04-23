# Granit.Identity.Federated.Cognito.BackgroundJobs

Recurring background job that re-runs the Phase 2 Cognito app-client group sync on
a 15-minute cadence (`*/15 * * * *`). Additive to the boot-time contributor — both
delegate to the same `CognitoClientRoleSyncService`, so the naming-prefix filter
(ADR-027) and orphan-policy dispatch (ADR-029) carry over unchanged.

## When to add it

- Deployments where Cognito group membership and names change without a host
  restart (AWS admin panel / IaC pipelines running out-of-band).
- Multi-region hosts where AWS-side mutations should propagate to every replica
  within ~15 minutes.

## Wiring

```csharp
services.AddGranitIdentityCognito();       // Phase 2 — boot-time sync + provider
// then in the host's modular registration:
services.AddTransient<GranitIdentityFederatedCognitoBackgroundJobsModule>();
```

`[RecurringJob]` discovery registers the job automatically at startup.
The sync itself is gated by `CognitoClientRoleSyncOptions.Enabled` — set to
`false` to short-circuit both the boot-time and recurring paths.

## ADR

- ADR-030: scheduled re-sync via `Granit.BackgroundJobs`.
