# Granit.Identity.Federated.BackgroundJobs

Background jobs for `Granit.Identity.Federated`. Ships a single recurring
`ClientRoleSyncJob` (default every 15 minutes — ADR-030) whose handler drives
**every** registered provider's `IClientRoleSyncPolicy` (Keycloak, Entra ID,
Cognito) through the shared `ClientRoleSyncEngine`, so drift between each
identity provider and the `RoleMetadata` store is bounded by the job cadence
rather than by host uptime.

This one package replaces the former per-provider `*.BackgroundJobs` packages:
the sync logic now lives once in `Granit.Identity.Federated` (the engine), each
provider registers only a thin policy, and this job fans out over all of them.
It is additive to the boot-time `ClientRoleSyncContributor` and inherits the
same idempotent upsert and orphan-policy semantics (ADR-029). With no provider
wired, the job is a no-op.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.BackgroundJobs
```
