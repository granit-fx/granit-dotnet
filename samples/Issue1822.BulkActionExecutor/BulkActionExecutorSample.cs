/// <summary>
/// SAMPLE IMPLEMENTATION — Bulk Action Executor for Invoice Module
/// 
/// This sample demonstrates the complete workflow for implementing a bulk action
/// using the new IEntityActionExecutor and IBulkActionExecutor interfaces
/// introduced in Issue #1822.
/// </summary>

using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using System.Text.Json;

namespace Granit.Invoicing.Actions.Samples;

// ============================================================================
// STEP 1: Define Domain Entity (simplified)
// ============================================================================

public sealed class Invoice
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum InvoiceStatus
{
    Draft = 0,
    Authorized = 1,
    Paid = 2,
    Archived = 3,
}

// ============================================================================
// STEP 2: Implement Single-Entity Executor
// ============================================================================

/// <summary>
/// Archives a single invoice. Validates state, updates status, commits changes.
/// Can be invoked via single-entity API or as fallback for bulk operations.
/// </summary>
public sealed class ArchiveInvoiceExecutor : IEntityActionExecutor<Invoice>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveInvoiceExecutor> _logger;
    private readonly TimeProvider _timeProvider;

    public ArchiveInvoiceExecutor(
        IUnitOfWork unitOfWork,
        ILogger<ArchiveInvoiceExecutor> logger,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<ActionResult> ExecuteAsync(
        Invoice invoice,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        // Validation: Cannot archive authorized invoices (business rule)
        if (invoice.Status == InvoiceStatus.Authorized)
        {
            _logger.LogWarning(
                "Cannot archive invoice {InvoiceNumber} — status is Authorized",
                invoice.Number);
            return ActionResult.Failure("Granit:Invoicing:CannotArchivePostAuthorized");
        }

        // Business Logic: Update state
        invoice.Status = InvoiceStatus.Archived;
        invoice.ArchivedAt = _timeProvider.GetUtcNow().DateTime;

        // Persist
        await _unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Invoice {InvoiceNumber} (ID: {InvoiceId}) archived successfully",
            invoice.Number,
            invoice.Id);

        return ActionResult.Success();
    }
}

// ============================================================================
// STEP 3: Implement Bulk-Optimized Executor (Optional)
// ============================================================================

/// <summary>
/// Bulk-optimized archive operation. Uses a single UPDATE statement instead of
/// N individual updates. Demonstrates the performance benefit of bulk executors.
/// 
/// Fallback: If this is not registered, the framework loops and calls
/// ArchiveInvoiceExecutor per-entity (less efficient but same result).
/// </summary>
public sealed class BulkArchiveInvoicesExecutor : IBulkActionExecutor<Invoice>
{
    private readonly InvoicingDbContext _db;
    private readonly ILogger<BulkArchiveInvoicesExecutor> _logger;
    private readonly TimeProvider _timeProvider;

    public BulkArchiveInvoicesExecutor(
        InvoicingDbContext db,
        ILogger<BulkArchiveInvoicesExecutor> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<BulkActionResult> ExecuteBulkAsync(
        IReadOnlyList<Invoice> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return BulkActionResult.Success(0);
        }

        var invoiceIds = entities.Select(e => e.Id).ToList();
        var archiveTime = _timeProvider.GetUtcNow().DateTime;

        try
        {
            // Pre-check: identify invoices that cannot be archived (Authorized status)
            var unauthorizableIds = await _db.Invoices
                .Where(i => invoiceIds.Contains(i.Id) && i.Status == InvoiceStatus.Authorized)
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);

            if (unauthorizableIds.Count > 0)
            {
                // Partial success: archive only the eligible ones
                int affected = await _db.Invoices
                    .Where(i => invoiceIds.Contains(i.Id) && i.Status != InvoiceStatus.Authorized)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(i => i.Status, InvoiceStatus.Archived)
                            .SetProperty(i => i.ArchivedAt, archiveTime),
                        cancellationToken);

                // Collect failures for the invoices that couldn't be archived
                var failures = new List<BulkFailure>();
                foreach (var id in unauthorizableIds)
                {
                    failures.Add(new BulkFailure(
                        id.ToString(),
                        "Granit:Invoicing:CannotArchivePostAuthorized"));
                }

                _logger.LogWarning(
                    "Bulk archive completed: {Affected} succeeded, {FailureCount} failed (Authorized status)",
                    affected,
                    failures.Count);

                return BulkActionResult.WithFailures(affected, failures.ToArray());
            }

            // All invoices are eligible — atomic bulk update
            int totalAffected = await _db.Invoices
                .Where(i => invoiceIds.Contains(i.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(i => i.Status, InvoiceStatus.Archived)
                        .SetProperty(i => i.ArchivedAt, archiveTime),
                    cancellationToken);

            _logger.LogInformation(
                "Bulk archive completed: {TotalAffected} invoices archived",
                totalAffected);

            return BulkActionResult.Success(totalAffected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk archive failed");

            // On critical failure, report all rows as failed
            var failures = entities
                .Select(e => new BulkFailure(e.Id.ToString(), ex.Message))
                .ToArray();

            return BulkActionResult.WithFailures(0, failures);
        }
    }
}

// ============================================================================
// STEP 4: Register Executors in DI
// ============================================================================

public static class InvoicingActionExtensions
{
    public static IServiceCollection AddInvoicingActions(
        this IServiceCollection services)
    {
        // Register single-entity executor
        services.AddScoped<ArchiveInvoiceExecutor>();

        // Register bulk-optimized executor (optional)
        services.AddScoped<BulkArchiveInvoicesExecutor>();

        return services;
    }
}

// ============================================================================
// STEP 5: Declare Action in Entity Definition
// ============================================================================

public sealed class InvoicingEntityDefinitions
{
    public static void ConfigureInvoiceActions(EntityDefinitionBuilder<Invoice> builder)
    {
        builder
            .Manifest(nameof(Invoice))
            .RouteBase("/api/invoices")
            
            // Declare the bulk-capable "archive" action
            .Action("archive")
                .Post()
                .DisplayKey("Invoice.Actions.Archive")
                .Icon("archive-box")
                .Confirmation("Invoice.Actions.ArchiveConfirmation")
                .Order(20)
                .OnSelection()  // Appears on selection bar (per-row or bulk)
                .RequiresPermission("Invoicing.Invoices.Manage")
                
                // NEW: Register server-side executor
                .ServerExecutor<ArchiveInvoiceExecutor>()
                
                // NEW: (Optional) Register bulk-optimized executor
                .BulkExecutor<BulkArchiveInvoicesExecutor>();
    }
}

// ============================================================================
// STEP 6: Client-Side Usage — TypeScript/React Example
// ============================================================================

/*
// Frontend Component (TypeScript / React)

import { BulkActionRequest, BulkActionResponse } from '@granit/entities';

const archiveSelectedInvoices = async (invoiceIds: string[]) => {
  if (invoiceIds.length === 0) return;

  try {
    // Call the bulk action endpoint
    const response = await fetch(`/api/entities/invoices/bulk/archive`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`,
      },
      body: JSON.stringify({
        ids: invoiceIds,
        payload: { archiveReason: 'Manual cleanup' },  // Action-specific payload
      }),
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const result: BulkActionResponse = await response.json();

    // Handle response
    if (result.failures.length === 0) {
      // All succeeded
      showSuccessToast(
        `✓ ${result.affected} invoices archived successfully!`
      );
      refreshInvoiceList();
    } else if (result.affected > 0) {
      // Partial success
      showWarningToast(
        `⚠ ${result.affected} archived; ${result.failures.length} failed:`
      );
      result.failures.forEach(f => {
        console.warn(`  Invoice ${f.entityId}: ${f.error}`);
      });
      refreshInvoiceList();
    } else {
      // All failed
      showErrorToast(
        `✗ All invoices failed to archive. See details:`
      );
      result.failures.forEach(f => {
        console.error(`  Invoice ${f.entityId}: ${f.error}`);
      });
    }
  } catch (error) {
    showErrorToast(`Failed to archive invoices: ${error.message}`);
  }
};

// Usage in a React component
export const InvoiceListToolbar: React.FC<InvoiceListToolbarProps> = ({
  selectedInvoiceIds,
  onAction,
}) => {
  return (
    <div className="toolbar">
      <button
        onClick={() => archiveSelectedInvoices(selectedInvoiceIds)}
        disabled={selectedInvoiceIds.length === 0}
      >
        Archive ({selectedInvoiceIds.length})
      </button>
    </div>
  );
};
*/

// ============================================================================
// STEP 7: API Endpoint Examples
// ============================================================================

/*
### Single-Entity Execution (Per-Row)

```
POST /api/invoices/{id}/archive
Content-Type: application/json

{
  "archiveReason": "Obsolete"
}

Response (Success):
200 OK
{ "isSuccess": true }

Response (Validation Failure):
400 Bad Request
{
  "isSuccess": false,
  "errorMessage": "Granit:Invoicing:CannotArchivePostAuthorized"
}
```

### Bulk Execution

```
POST /api/entities/invoices/bulk/archive
Content-Type: application/json

{
  "ids": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002",
    "00000000-0000-0000-0000-000000000003"
  ],
  "payload": {
    "archiveReason": "Quarterly cleanup"
  }
}

Response (Full Success):
200 OK
{
  "affected": 3,
  "failures": []
}

Response (Partial Success):
200 OK
{
  "affected": 2,
  "failures": [
    {
      "entityId": "00000000-0000-0000-0000-000000000002",
      "error": "Granit:Invoicing:CannotArchivePostAuthorized"
    }
  ]
}
```
*/

// ============================================================================
// STEP 8: Testing
// ============================================================================

public sealed class ArchiveInvoiceExecutorTests
{
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ILogger<ArchiveInvoiceExecutor> _loggerMock;
    private readonly TimeProvider _timeProviderMock;
    private readonly ArchiveInvoiceExecutor _executor;

    public ArchiveInvoiceExecutorTests()
    {
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _loggerMock = Substitute.For<ILogger<ArchiveInvoiceExecutor>>();
        _timeProviderMock = Substitute.For<TimeProvider>();

        _executor = new ArchiveInvoiceExecutor(_unitOfWorkMock, _loggerMock, _timeProviderMock);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivesDraft_Success()
    {
        // Arrange
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = "INV-001",
            Status = InvoiceStatus.Draft,
            Amount = 1000m,
        };

        var payload = JsonSerializer.SerializeToElement(new { });
        _unitOfWorkMock.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        var result = await _executor.ExecuteAsync(invoice, payload, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Archived, invoice.Status);
        Assert.NotNull(invoice.ArchivedAt);
        await _unitOfWorkMock.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizedInvoice_Failure()
    {
        // Arrange
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = "INV-002",
            Status = InvoiceStatus.Authorized,
            Amount = 2000m,
        };

        var payload = JsonSerializer.SerializeToElement(new { });

        // Act
        var result = await _executor.ExecuteAsync(invoice, payload, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Granit:Invoicing:CannotArchivePostAuthorized", result.ErrorMessage);
        Assert.Equal(InvoiceStatus.Authorized, invoice.Status);  // Unchanged
        await _unitOfWorkMock.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

// ============================================================================
// NOTES
// ============================================================================

/*
KEY DESIGN DECISIONS:

1. **Per-Row Failure Isolation**: Bulk operations succeed partially.
   - Reason: UX — users don't lose work when one row fails in 1000.
   - Fallback: Executors manage their own transactions; atomic bulks require explicit logic.

2. **Dual-Executor Pattern**: Single-entity + (optional) bulk executor.
   - Reason: Allows per-row for correctness, bulk for performance.
   - Fallback: Framework loops per-entity if bulk executor not registered.

3. **Framework-Emitted Events**: EntityBulkUpdatedEvent<T> auto-emitted.
   - Reason: Ensures consistent cache invalidation (story #1794).
   - Executor focus: Business logic only, not boilerplate.

4. **Opt-in Server Execution**: .ServerExecutor<T>() must be called.
   - Reason: Backward compatibility — existing frontend-only actions unaffected.
   - Security: Explicit intent; no implicit behavior changes.

NEXT STEPS (Future Issues):

- Story #1794: EntityBulkUpdatedEvent emission and cache invalidation
- Architecture test: Validate every RequiresServerExecution action has impl
- Long-running bulk: Background job dispatch (out of scope)
- Undo/rollback UI: Transactional semantics with rollback API
*/
