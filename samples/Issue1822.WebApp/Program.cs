using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(cfg => cfg.AddSimpleConsole());

// In-memory invoice store and executors
builder.Services.AddSingleton<InMemoryInvoiceRepository>();
builder.Services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
builder.Services.AddScoped<ArchiveInvoiceExecutor>();
builder.Services.AddScoped<BulkArchiveInvoicesExecutor>();

var app = builder.Build();

app.MapGet("/api/invoices/seed", (InMemoryInvoiceRepository repo) =>
{
    repo.SeedSample();
    return Results.Ok(new { seeded = repo.Count });
});

app.MapPost("/api/entities/invoices/bulk/archive", async (HttpContext http, InMemoryInvoiceRepository repo, IServiceProvider sp) =>
{
    var doc = await JsonDocument.ParseAsync(http.Request.Body);
    var root = doc.RootElement;

    var ids = root.GetProperty("ids").EnumerateArray().Select(e => e.GetGuid()).ToList();
    var invoices = repo.GetByIds(ids);

    var bulkExecutor = sp.GetService<BulkArchiveInvoicesExecutor>();
    if (bulkExecutor is not null)
    {
        var result = await bulkExecutor.ExecuteBulkAsync(invoices, root.GetProperty("payload"), http.RequestAborted);
        return Results.Ok(new { affected = result.AffectedCount, failures = result.Failures });
    }

    var executor = sp.GetRequiredService<ArchiveInvoiceExecutor>();
    var failures = new List<BulkFailure>();
    int affected = 0;
    foreach (var inv in invoices)
    {
        var r = await executor.ExecuteAsync(inv, root.GetProperty("payload"), http.RequestAborted);
        if (r.IsSuccess) affected++; else failures.Add(new BulkFailure(inv.Id.ToString(), r.ErrorMessage ?? ""));
    }

    return Results.Ok(new { affected, failures });
});

app.MapGet("/api/invoices", (InMemoryInvoiceRepository repo) => Results.Ok(repo.List()));

app.Run("http://localhost:5005");

// --- Minimal implementations below ---

public sealed class Invoice
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum InvoiceStatus { Draft = 0, Authorized = 1, Paid = 2, Archived = 3 }

public interface IUnitOfWork { Task CommitAsync(CancellationToken ct = default); }
public sealed class InMemoryUnitOfWork : IUnitOfWork { public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask; }

public sealed class InMemoryInvoiceRepository
{
    private readonly List<Invoice> _list = new();
    public int Count => _list.Count;
    public void SeedSample()
    {
        _list.Clear();
        _list.Add(new Invoice { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Number = "INV-001", Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow });
        _list.Add(new Invoice { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Number = "INV-002", Status = InvoiceStatus.Authorized, CreatedAt = DateTime.UtcNow });
        _list.Add(new Invoice { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Number = "INV-003", Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow });
    }
    public List<Invoice> GetByIds(IEnumerable<Guid> ids) => _list.Where(i => ids.Contains(i.Id)).ToList();
    public IReadOnlyList<Invoice> List() => _list.AsReadOnly();
}

public record ActionResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static ActionResult Success() => new(true);
    public static ActionResult Failure(string key) { ArgumentException.ThrowIfNullOrWhiteSpace(key); return new(false, key); }
}

public sealed record BulkFailure(string EntityId, string ErrorMessage);
public sealed record BulkActionResult(int AffectedCount, IReadOnlyList<BulkFailure> Failures)
{
    public static BulkActionResult Success(int c) => new(c, Array.Empty<BulkFailure>());
    public static BulkActionResult WithFailures(int c, params BulkFailure[] f) => new(c, f);
}

public sealed class ArchiveInvoiceExecutor
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ArchiveInvoiceExecutor> _logger;
    private readonly TimeProvider _time;
    public ArchiveInvoiceExecutor(IUnitOfWork uow, ILogger<ArchiveInvoiceExecutor> logger, TimeProvider time) { _uow = uow; _logger = logger; _time = time; }
    public Task<ActionResult> ExecuteAsync(Invoice invoice, JsonElement payload, CancellationToken cancellationToken)
    {
        if (invoice.Status == InvoiceStatus.Authorized)
        {
            _logger.LogWarning("Cannot archive {Number}", invoice.Number);
            return Task.FromResult(ActionResult.Failure("Granit:Invoicing:CannotArchivePostAuthorized"));
        }
        invoice.Status = InvoiceStatus.Archived;
        invoice.ArchivedAt = _time.GetUtcNow().DateTime;
        return Task.FromResult(ActionResult.Success());
    }
}

public sealed class BulkArchiveInvoicesExecutor
{
    private readonly ILogger<BulkArchiveInvoicesExecutor> _logger;
    public BulkArchiveInvoicesExecutor(ILogger<BulkArchiveInvoicesExecutor> logger) { _logger = logger; }

    public Task<BulkActionResult> ExecuteBulkAsync(IReadOnlyList<Invoice> entities, JsonElement payload, CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return Task.FromResult(BulkActionResult.Success(0));

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
        if (failures.Count > 0) return Task.FromResult(BulkActionResult.WithFailures(affected, failures.ToArray()));
        return Task.FromResult(BulkActionResult.Success(affected));
    }
}
