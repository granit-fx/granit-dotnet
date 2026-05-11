using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Granit.Authorization;
using Granit.Entities;
using Granit.Entities.Extensions;
using Granit.Entities.Actions.Execution;
using Granit.Entities.Endpoints.Extensions;
using Granit.Entities.Internal.BulkActions;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(cfg => cfg.AddSimpleConsole());
builder.Services.AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPermissionChecker, AllowAllPermissionChecker>();
builder.Services.AddFusionCache().WithSystemTextJsonSerializer();

builder.Services.AddGranitEntities();
builder.Services.AddGranitEntitiesEndpoints();
builder.Services.AddEntityDefinition<InvoicingEntity, InvoicingEntityDefinition>();

builder.Services.AddDbContext<InvoicingDbContext>(opt => opt.UseSqlite("Data Source=issue1822.db"));
builder.Services.AddScoped<IDbContextFactory<DbContext>, DbContextFactoryAdapter>();
builder.Services.AddScoped<BulkActionExecutionOrchestrator>();

builder.Services.AddScoped<ArchiveInvoiceExecutor>();
builder.Services.AddScoped<BulkArchiveInvoicesExecutor>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/invoices/seed", async (IServiceProvider sp) =>
{
    using IServiceScope scope = sp.CreateScope();
    InvoicingDbContext db = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();

    db.Invoices.AddRange(
        new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Number = "INV-001", Status = InvoiceStatus.Draft },
        new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Number = "INV-002", Status = InvoiceStatus.Authorized },
        new InvoicingEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Number = "INV-003", Status = InvoiceStatus.Draft });
    await db.SaveChangesAsync();
    return TypedResults.Ok(new { seeded = await db.Invoices.CountAsync() });
});

app.MapGranitEntitiesEndpoints("/api/entities");
app.MapGet("/api/invoices", async (InvoicingDbContext db) => TypedResults.Ok(await db.Invoices.ToListAsync()));

app.Run("http://localhost:5005");

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

        List<BulkFailure> failures = [];
        int affected = 0;

        foreach (InvoicingEntity e in entities)
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

        if (failures.Count > 0) return BulkActionResult.WithFailures(affected, [.. failures]);
        return BulkActionResult.Success(affected);
    }
}

public sealed class InvoicingEntityDefinition : EntityDefinition<InvoicingEntity>
{
    public override string Name => "invoices";

    protected override void Configure(EntityDefinitionBuilder<InvoicingEntity> builder)
    {
        builder
            .DisplayKey("Invoicing.Invoice")
            .Icon("receipt")
            .RouteBase("/api/entities/invoices");

        builder.Action("archive", action => action
            .Post("archive")
            .DisplayKey("Invoice.Actions.Archive")
            .Icon("archive-box")
            .Order(20)
            .Confirmation("Invoice.Actions.ArchiveConfirmation")
            .OnSelection()
            .ServerExecutor<ArchiveInvoiceExecutor>()
            .BulkExecutor<BulkArchiveInvoicesExecutor>());
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, "sample-user"), new(ClaimTypes.Name, "sample-user")];
        ClaimsIdentity identity = new(claims, Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class AllowAllPermissionChecker : IPermissionChecker
{
    public Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetGrantedAsync(
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default)
        => Task.FromResult(permissionNames);
}

public sealed class DbContextFactoryAdapter : IDbContextFactory<DbContext>
{
    private readonly DbContextOptions<InvoicingDbContext> _options;

    public DbContextFactoryAdapter(DbContextOptions<InvoicingDbContext> options)
    {
        _options = options;
    }

    public DbContext CreateDbContext() => new InvoicingDbContext(_options);

    public async Task<DbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => await Task.FromResult<DbContext>(new InvoicingDbContext(_options));
}
