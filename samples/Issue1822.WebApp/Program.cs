using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Granit.Entities.Actions.Execution;
using Granit.Entities.Internal.BulkActions;
using Granit.Entities.Actions;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(cfg => cfg.AddSimpleConsole());

// In-memory invoice store and executors
// Use real orchestrator from Granit and EF Core-backed DbContext
builder.Services.AddDbContext<InvoicingDbContext>(opt => opt.UseSqlite("Data Source=issue1822.db"));
builder.Services.AddScoped<BulkActionExecutionOrchestrator>();

builder.Services.AddScoped<ArchiveInvoiceExecutor>();
builder.Services.AddScoped<BulkArchiveInvoicesExecutor>();

var app = builder.Build();

app.MapGet("/api/invoices/seed", async (IServiceProvider sp) =>
{
    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();

    db.Invoices.AddRange(new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Number = "INV-001", Status = InvoiceStatus.Draft },
                         new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Number = "INV-002", Status = InvoiceStatus.Authorized },
                         new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Number = "INV-003", Status = InvoiceStatus.Draft });
    await db.SaveChangesAsync();
    return TypedResults.Ok(new { seeded = await db.Invoices.CountAsync() });
});

app.MapPost("/api/entities/invoices/bulk/archive", async (HttpContext http, IServiceProvider sp) =>
{
    var doc = await JsonDocument.ParseAsync(http.Request.Body);
    var root = doc.RootElement;
    var ids = root.GetProperty("ids").EnumerateArray().Select(e => e.GetGuid()).ToList();

    using var scope = sp.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();

    // Build descriptor for the action (server executor + bulk executor types)
    var descriptor = new EntityActionDescriptor(
        Name: "archive",
        Kind: EntityActionKind.ApiCall,
        DisplayKey: "Invoice.Actions.Archive",
        Icon: "archive-box",
        Order: 20,
        RequiresPermission: "Invoicing.Invoices.Manage",
        UrlTemplate: "/api/invoices/{id}/archive",
        HttpMethod: "POST",
        ConfirmationKey: "Invoice.Actions.ArchiveConfirmation",
        WorkflowTransitionName: null,
        ContributorAssemblyName: null,
        ShowOnKanbanCard: false,
        ShowOnGalleryCard: false,
        ShowOnCalendarTile: false,
        ShowOnListHeader: false,
        ShowOnSelection: true);

    // Set server/bulk executor types via reflection (properties added in feature)
    typeof(EntityActionDescriptor).GetProperty("ServerExecutorType")?.SetValue(descriptor, typeof(ArchiveInvoiceExecutor));
    typeof(EntityActionDescriptor).GetProperty("BulkExecutorType")?.SetValue(descriptor, typeof(BulkArchiveInvoicesExecutor));

    var orchestrator = scope.ServiceProvider.GetRequiredService<BulkActionExecutionOrchestrator>();
    var result = await orchestrator.ExecuteAsync<InvoicingEntity>(descriptor, db, ids.Select(g => g.ToString()).ToList(), root.GetProperty("payload"), http.RequestAborted);

    return TypedResults.Ok(new { affected = result.AffectedCount, failures = result.Failures });
});

app.MapGet("/api/invoices", async (InvoicingDbContext db) => TypedResults.Ok(await db.Invoices.ToListAsync()));

app.Run("http://localhost:5005");

// --- EF Core-backed implementations for the sample ---

public enum InvoiceStatus { Draft = 0, Authorized = 1, Paid = 2, Archived = 3 }

public class InvoicingEntity
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvoicingDbContext : DbContext
{
    public InvoicingDbContext(DbContextOptions<InvoicingDbContext> options) : base(options) { }
    public DbSet<InvoicingEntity> Invoices => Set<InvoicingEntity>();
}

public sealed class ArchiveInvoiceExecutor : IEntityActionExecutor<InvoicingEntity>
{
    private readonly InvoicingDbContext _db;
    private readonly ILogger<ArchiveInvoiceExecutor> _logger;
    public ArchiveInvoiceExecutor(InvoicingDbContext db, ILogger<ArchiveInvoiceExecutor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAsync(InvoicingEntity invoice, JsonElement payload, CancellationToken cancellationToken)
    {
        if (invoice.Status == InvoiceStatus.Authorized)
        {
            _logger.LogWarning("Cannot archive {Number}", invoice.Number);
            return ActionResult.Failure("Granit:Invoicing:CannotArchivePostAuthorized");
        }

        invoice.Status = InvoiceStatus.Archived;
        invoice.ArchivedAt = DateTime.UtcNow;
        _db.Update(invoice);
        await _db.SaveChangesAsync(cancellationToken);
        return ActionResult.Success();
    }
}

public sealed class BulkArchiveInvoicesExecutor : IBulkActionExecutor<InvoicingEntity>
{
    private readonly InvoicingDbContext _db;
    private readonly ILogger<BulkArchiveInvoicesExecutor> _logger;

    public BulkArchiveInvoicesExecutor(InvoicingDbContext db, ILogger<BulkArchiveInvoicesExecutor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BulkActionResult> ExecuteBulkAsync(IReadOnlyList<InvoicingEntity> entities, JsonElement payload, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return BulkActionResult.Success(0);

        var failures = new List<BulkFailure>();
        int affected = 0;

        foreach (var e in entities)
        {
            if (e.Status == InvoiceStatus.Authorized)
            {
                failures.Add(new BulkFailure(e.Id.ToString(), "Granit:Invoicing:CannotArchivePostAuthorized"));
                continue;
            }
            e.Status = InvoiceStatus.Archived;
            e.ArchivedAt = DateTime.UtcNow;
            affected++;
        }

        _db.UpdateRange(entities);
        await _db.SaveChangesAsync(cancellationToken);

        if (failures.Count > 0) return BulkActionResult.WithFailures(affected, failures.ToArray());
        return BulkActionResult.Success(affected);
    }
}
