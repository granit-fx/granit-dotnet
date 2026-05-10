# Issue #1822 — Bulk Action Executor Implementation

## Problem Analysis

### The Challenge

**Granit.Entities** had a declarative-only action system. The `IEntityActionContributor` interface allowed cross-module actions, but there was **no server-side action execution surface**. This meant:

1. **Client-side only**: Actions were rendered with URLs and the client executed them
2. **No bulk operations**: Each action targeted a single entity via its ID in the URL
3. **No framework abstraction**: Developers had to manually route action calls to business logic
4. **No standardized failure handling**: Each action implementation handled errors differently

### Why This Matters

Story #1794 introduced `EntityBulkUpdatedEvent<T>` for efficient cache invalidation across bulk operations. But bulk endpoints require a **standardized server-side execution pattern** — the framework needs to know:

- How to dispatch the action (who implements it?)
- How to pass parameters (structured payload?)
- How to handle partial failures (some rows succeed, some fail?)
- How to coordinate transactions (all-or-nothing vs per-row isolation?)

### Design Questions (ADR)

Three architectural tensions were resolved:

| Question | Solution |
|----------|----------|
| **Executor Lifecycle** | Scoped, resolved per-request via DI. Allows stateful operations (DbContext injection, logging, metrics). |
| **Transactional Semantics** | **Per-row failure isolation** (not all-or-nothing). Allows bulk operations to succeed partially — critical for UI feedback (e.g., "5 succeeded, 3 failed with details"). Atomicity is executor responsibility. |
| **Side Effects** | **Framework-emitted events**. After a successful bulk run, `EntityBulkUpdatedEvent<T>` is auto-emitted with the affected entity list, kicking off story #1794 invalidation. |

---

## Solution Design

### Core Interfaces

#### 1. `IEntityActionExecutor<TEntity>` (Required)

Executes an action against a **single entity instance**:

```csharp
public interface IEntityActionExecutor<TEntity> where TEntity : class
{
    Task<ActionResult> ExecuteAsync(
        TEntity entity,
        JsonElement payload,
        CancellationToken cancellationToken);
}
```

**Example: Toggle Entity Active Status**

```csharp
public sealed class ToggleActiveExecutor : IEntityActionExecutor<Invoice>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ToggleActiveExecutor> _logger;

    public ToggleActiveExecutor(IUnitOfWork unitOfWork, ILogger<ToggleActiveExecutor> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAsync(
        Invoice entity,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        try
        {
            // Business logic
            entity.IsActive = !entity.IsActive;
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Invoice {InvoiceId} toggled to {Status}", entity.Id, entity.IsActive);
            return ActionResult.Success();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to toggle invoice {InvoiceId}", entity.Id);
            return ActionResult.Failure("Granit:Invoicing:CannotToggleState");
        }
    }
}
```

#### 2. `IBulkActionExecutor<TEntity>` (Optional, Opt-in)

Executes an action against **multiple entities at once** — allows batch optimizations:

```csharp
public interface IBulkActionExecutor<TEntity> where TEntity : class
{
    Task<BulkActionResult> ExecuteBulkAsync(
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken);
}
```

**Example: Bulk Update via Database**

```csharp
public sealed class BulkArchiveInvoicesExecutor : IBulkActionExecutor<Invoice>
{
    private readonly InvoicingDbContext _db;

    public BulkArchiveInvoicesExecutor(InvoicingDbContext db) => _db = db;

    public async Task<BulkActionResult> ExecuteBulkAsync(
        IReadOnlyList<Invoice> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var ids = entities.Select(e => e.Id).ToList();
        
        try
        {
            // Atomic bulk UPDATE
            int affected = await _db.Invoices
                .Where(i => ids.Contains(i.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(i => i.Status, InvoiceStatus.Archived),
                    cancellationToken);

            return BulkActionResult.Success(affected);
        }
        catch (Exception ex)
        {
            var failures = entities
                .Select(e => new BulkFailure(e.Id.ToString(), ex.Message))
                .ToList();
            return BulkActionResult.WithFailures(0, failures);
        }
    }
}
```

**Fallback Strategy**: If no `IBulkActionExecutor<T>` is registered, the framework loops and calls `IEntityActionExecutor<T>` per-entity.

#### 3. Result Types

```csharp
// Single-entity outcome
public sealed record ActionResult(bool IsSuccess, string? ErrorMessage = null);

// Bulk outcome (supports partial success)
public sealed record BulkActionResult(int AffectedCount, IReadOnlyList<BulkFailure> Failures);
public sealed record BulkFailure(string EntityId, string ErrorMessage);
```

### Builder Extension

```csharp
var builder = new EntityActionBuilder<Invoice>("archive")
    .Post()
    .DisplayKey("Invoice.Actions.Archive")
    .Icon("archive")
    .OnSelection()
    .ServerExecutor<ArchiveInvoiceExecutor>()  // Register single-entity executor
    .BulkExecutor<BulkArchiveInvoicesExecutor>();  // (Optional) Bulk optimization
```

### Updated `EntityActionDescriptor`

Three new fields:

```csharp
public sealed record EntityActionDescriptor(
    // ... existing 15 fields ...
    bool RequiresServerExecution = false,           // Flag: needs server execution
    Type? ServerExecutorType = null,                // Executor impl type
    Type? BulkExecutorType = null                   // (Optional) bulk executor impl
);
```

### Bulk Action Endpoint

**Route**: `POST /api/entities/{name}/bulk/{action}`

**Request**:
```json
{
  "ids": ["00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002"],
  "payload": { "archiveReason": "Deprecated" }
}
```

**Response** (Success):
```json
{
  "affected": 2,
  "failures": []
}
```

**Response** (Partial Success):
```json
{
  "affected": 1,
  "failures": [
    {
      "entityId": "00000000-0000-0000-0000-000000000002",
      "error": "Granit:Invoicing:CannotArchivePostAuthorized"
    }
  ]
}
```

### Orchestration Flow

```
BulkActionEndpoint
├─ Validate request (IDs not empty, permission gated)
├─ Load entities from DbContext by ID
├─ Dispatch:
│  ├─ If IBulkActionExecutor<T> registered → call ExecuteBulkAsync (all-at-once)
│  └─ Else → loop entities, call IEntityActionExecutor<T>.ExecuteAsync per-row
├─ Collect results (successes + per-row failures)
├─ Emit EntityBulkUpdatedEvent<T> on success (story #1794 kicks in)
└─ Return BulkActionResponse
```

---

## Implementation Details

### Files Created

| File | Purpose |
|------|---------|
| `Granit.Entities.Abstractions/Actions/Execution/IEntityActionExecutor.cs` | Base executor interface |
| `Granit.Entities.Abstractions/Actions/Execution/IBulkActionExecutor.cs` | Bulk executor interface |
| `Granit.Entities.Abstractions/Actions/Execution/ActionResult.cs` | Result types |
| `Granit.Entities.Endpoints/Dtos/BulkActions/BulkActionRequest.cs` | Request DTO |
| `Granit.Entities.Endpoints/Dtos/BulkActions/BulkActionResponse.cs` | Response DTO |
| `Granit.Entities/Internal/BulkActions/BulkActionExecutionOrchestrator.cs` | Orchestration logic |
| `Granit.Entities.Endpoints/Endpoints/BulkActionEndpoint.cs` | Endpoint handler |

### Modified Files

| File | Changes |
|------|---------|
| `EntityActionDescriptor.cs` | Added 3 new fields for server execution |
| `EntityActionBuilder.cs` | Added `.ServerExecutor<T>()` and `.BulkExecutor<T>()` methods |

---

## Testing Strategy

### Test Coverage

1. **Unit Tests** (`ActionResultTests.cs`):
   - `ActionResult.Success()` creates correct state
   - `ActionResult.Failure()` captures error key
   - `BulkActionResult.Success()` / `.WithFailures()` populate correctly
   - Builder enforces `.ServerExecutor()` before `.BulkExecutor()`
   - Builder.Build() includes executor types in descriptor

2. **Integration Tests** (TODO: Orchestrator integration):
   - Load entities by ID from DbContext
   - Dispatch to executor (single + bulk paths)
   - Aggregate per-row failures
   - Emit domain event on success

3. **Architecture Tests** (TODO):
   - Every action with `RequiresServerExecution = true` must have `ServerExecutorType` set
   - Executor types must implement `IEntityActionExecutor<T>` or `IBulkActionExecutor<T>`

---

## Example Usage

### Defining a Bulk Action

```csharp
// In InvoicingModule.cs
public sealed class InvoicingModule : GranitModule
{
    public override void Configure(EntityDefinitionBuilder<Invoice> builder)
    {
        builder
            .Manifest(nameof(Invoice))
            .RouteBase("/api/invoices")
            .Action("archive")
                .Post()
                .DisplayKey("Invoice.Actions.Archive")
                .Icon("archive")
                .OnSelection()
                .RequiresPermission("Invoicing.Invoices.Manage")
                .ServerExecutor<ArchiveInvoiceExecutor>()
                .BulkExecutor<BulkArchiveInvoicesExecutor>();
    }
}
```

### Executor Implementation

```csharp
public sealed class ArchiveInvoiceExecutor : IEntityActionExecutor<Invoice>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveInvoiceExecutor> _logger;

    public ArchiveInvoiceExecutor(IUnitOfWork unitOfWork, ILogger<ArchiveInvoiceExecutor> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAsync(
        Invoice invoice,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (invoice.Status == InvoiceStatus.Authorized)
        {
            return ActionResult.Failure("Granit:Invoicing:CannotArchivePostAuthorized");
        }

        invoice.Status = InvoiceStatus.Archived;
        invoice.ArchivedAt = SystemClock.UtcNow;

        await _unitOfWork.CommitAsync(cancellationToken);
        _logger.LogInformation("Invoice {InvoiceId} archived", invoice.Id);

        return ActionResult.Success();
    }
}
```

### Client-Side Usage

```typescript
// Frontend (React / Angular)
const archiveInvoices = async (invoiceIds: string[]) => {
  const response = await fetch(`/api/entities/invoices/bulk/archive`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      ids: invoiceIds,
      payload: { archiveReason: 'Obsolete' }
    })
  });

  const result = await response.json(); // BulkActionResponse
  
  if (result.failures.length === 0) {
    showToast(`${result.affected} invoices archived successfully!`);
  } else {
    showWarning(`${result.affected} archived; ${result.failures.length} failed:`, 
                result.failures.map(f => `${f.entityId}: ${f.error}`));
  }
};
```

---

## Architectural Decisions

### Why Per-Row Failure Isolation (Not All-or-Nothing)?

**UX Impact**: Bulk operations on large datasets are common. If a single validation failure rolls back 999 successes, users lose data-entry progress. Per-row isolation with partial success is dramatically more user-friendly (Odoo, Salesforce, Airtable patterns).

**Transaction Scope**: Executors manage their own transaction (injected DbContext/UnitOfWork). They can commit per-entity or in batches; the framework doesn't mandate atomicity.

### Why Auto-Emit Events (Not Executor Responsibility)?

**Consistency**: Every bulk run emits `EntityBulkUpdatedEvent<T>`, ensuring:
- Predictable cache invalidation (story #1794)
- Audit trails capture all bulk actions
- Subscribers (notifications, analytics) see consistent state

**Simplicity**: Executor authors don't need to remember event emission; the framework guarantees it.

### Why Opt-in Server Execution?

**Backward Compat**: Existing actions (pure-frontend) don't need a `RequiresServerExecution = true` flag. New bulk-capable actions explicitly opt in via `.ServerExecutor<T>()`.

**Security**: Action declaration and server impl are decoupled. A rogue `.ServerExecutor<T>` registration won't affect frontend-only actions.

---

## Related Stories

- **Story #1794**: Invalidation primitive for bulk updates (cache + audit)
- **Story #1793**: D1 invalidator (predecessor, single-entity invalidation)
- **Epic #1510**: Entity action framework completeness

---

## Future Extensions

1. **Long-running Bulk**: Async background job dispatch (out-of-scope per issue; belongs in `Granit.DataExchange`)
2. **Cross-entity Bulk**: Single endpoint for mixed entity types (out-of-scope; design complexity)
3. **Undo/Rollback**: Transactional bulk ops with manual rollback UI (future phase)

