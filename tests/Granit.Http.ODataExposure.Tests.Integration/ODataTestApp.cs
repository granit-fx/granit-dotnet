using Granit.Authorization;
using Granit.Http.ODataExposure.Extensions;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Builds an in-memory minimal-API host wired with the OData exposure layer
/// and the test DbContext, both backed by the supplied PostgreSQL connection
/// string. Each request swaps the active tenant via the
/// <c>X-Test-Tenant</c> header so attack scenarios switch identity without
/// rebuilding the host.
/// </summary>
/// <remarks>
/// The integration uses Granit's production <see cref="CurrentTenant"/>
/// (AsyncLocal-based, registered as singleton) — NOT a per-scope stub.
/// <c>ApplyGranitConventions</c> captures the <see cref="ICurrentTenant"/>
/// instance into the EF Core model filter expression, and EF Core caches
/// the model PER DBCONTEXT TYPE. A per-scope stub would let the first
/// request's stub leak into every subsequent query's filter; only an
/// AsyncLocal-backed singleton has the correct semantics.
/// </remarks>
internal sealed class ODataTestApp : IAsyncDisposable
{
    private readonly WebApplication _app;
    public HttpClient Client { get; }
    public SqlCaptureSink SqlCapture { get; }

    private ODataTestApp(WebApplication app, HttpClient client, SqlCaptureSink sqlCapture)
    {
        _app = app;
        Client = client;
        SqlCapture = sqlCapture;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        Client.Dispose();
    }

    public static async Task<ODataTestApp> CreateAsync(string connectionString)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        SqlCaptureSink sqlCapture = new();

        builder.Services.AddSingleton<ICurrentTenant, CurrentTenant>();
        builder.Services.AddSingleton<IPermissionChecker, StubPermissionChecker>();
        builder.Services.AddSingleton(sqlCapture);

        builder.Services.AddGranitQueryEngine();
        builder.Services.AddScoped(typeof(IQueryEngine<>),
            typeof(QueryEngine.EntityFrameworkCore.GranitQueryEngineEntityFrameworkCoreModule).Assembly
                .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!);
        builder.Services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();

        builder.Services.AddDbContext<TestDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(connectionString);
            opt.LogTo(sqlCapture.Capture, LogLevel.Information,
                DbContextLoggerOptions.SingleLine);
        });

        builder.Services.AddScoped<IQueryableSource<Invoice>, InvoiceSource>();
        builder.Services.AddGranitODataExposure();

        WebApplication app = builder.Build();

        // Tenant-switching middleware — read X-Test-Tenant header, scope the
        // change to the request's lifetime via using/Dispose so AsyncLocal
        // restores cleanly even if downstream throws.
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Tenant", out Microsoft.Extensions.Primitives.StringValues header)
                && Guid.TryParse(header.ToString(), out Guid tenantId))
            {
                ICurrentTenant currentTenant = context.RequestServices.GetRequiredService<ICurrentTenant>();
                using IDisposable tenantScope = currentTenant.Change(tenantId, name: $"Tenant-{tenantId}");
                await next(context).ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });

        app.MapGranitODataEndpoints("/api/granit/odata", opts =>
            opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices"));

        await app.StartAsync().ConfigureAwait(false);

        // Apply schema once per host (xUnit fixture lifetime). Done outside
        // any tenant scope so the first model build pins the singleton
        // CurrentTenant — subsequent requests mutate its AsyncLocal, never
        // the captured instance.
        using IServiceScope dbScope = app.Services.CreateScope();
        TestDbContext db = dbScope.ServiceProvider.GetRequiredService<TestDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        return new ODataTestApp(app, app.GetTestClient(), sqlCapture);
    }

    /// <summary>
    /// Resolves a fresh <see cref="TestDbContext"/> outside any tenant scope —
    /// used by tests to seed and verify rows directly. Caller must dispose.
    /// </summary>
    public IServiceScope CreateScope() => _app.Services.CreateScope();
}
