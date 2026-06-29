# Granit.Workflow.EntityFrameworkCore

EF Core integration for `Granit.Workflow`. Provides `WorkflowTransitionInterceptor`
for automatic ISO 27001-compliant audit trail creation, `IWorkflowDbContext` for host
integration, and `IPublishable` query filter synchronization.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workflow.EntityFrameworkCore
```

## Dependencies

- `Granit.Persistence`
- `Granit.Workflow`

## Usage

1. The host DbContext must implement `IWorkflowDbContext`, exposing the
   transition-record set:

   ```csharp
   public DbSet<WorkflowTransitionRecord> WorkflowTransitionRecords => Set<WorkflowTransitionRecord>();
   ```

2. Emit the `workflow_transition_records` table in `OnModelCreating`
   (or `OnGranitModelCreating`):

   ```csharp
   modelBuilder.ConfigureWorkflowModule();
   ```

3. Register the EF Core services (interceptor + `IWorkflowHistoryQuery` +
   `IWorkflowTransitionRecorder` + queryable source):

   ```csharp
   services.AddGranitWorkflowEntityFrameworkCore<HostDbContext>();
   ```

The `WorkflowTransitionInterceptor` must be ordered after
`AuditedEntityInterceptor` and before `SoftDeleteInterceptor` in the interceptor
chain.

If tenant-scoped workflow records are desired, also call
`modelBuilder.ConfigureWorkflowModule()` in the tenant DbContext's
`OnModelCreating` and register the matching
`AddGranitWorkflowEntityFrameworkCore<TenantDbContext>()`.

## Documentation

See the [full documentation](https://granit-fx.dev).
