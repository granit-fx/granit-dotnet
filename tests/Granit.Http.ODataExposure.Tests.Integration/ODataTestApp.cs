using Granit.Authorization;
using Granit.DataExchange.Export;
using Granit.Entities;
using Granit.Http.ODataExposure.Extensions;
using Granit.Http.ODataExposure.Options;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.RateLimiting.Extensions;
using Granit.RateLimiting.Options;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
/// <para>
/// The integration uses Granit's production <see cref="CurrentTenant"/>
/// (AsyncLocal-based, registered as a singleton) behind a <see cref="TestDbContext"/>
/// that inherits <c>GranitDbContext</c>. The multi-tenant filter is parameterised
/// (<c>@ef_filter__CurrentTenantId</c>) and re-bound per request from the live
/// AsyncLocal value, so swapping the active tenant via the <c>X-Test-Tenant</c>
/// header takes effect on every query instead of being constant-folded into the
/// cached model at first build.
/// </para>
/// <para>
/// #3005 additions: a header-driven test authentication scheme
/// (<see cref="TestAuthenticationHandler"/>, <c>X-Test-User</c>) plus a
/// configurable permission predicate so the $metadata authorization-stance
/// matrix can drive 401 / 403 / 200 without a real identity stack. The
/// mount declares <c>AllowAnonymousMetadata()</c> by default (pre-#3005
/// behaviour for every legacy suite); pass <c>configureOptions</c>
/// to override the stance or add extra EntitySets.
/// </para>
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

    public static Task<ODataTestApp> CreateAsync(string connectionString)
        => CreateAsync(connectionString, configureEntitySet: null, rateLimitPermitLimit: null);

    public static Task<ODataTestApp> CreateAsync(
        string connectionString,
        Action<ODataEntitySetBuilder<Invoice>>? configureEntitySet)
        => CreateAsync(connectionString, configureEntitySet, rateLimitPermitLimit: null);

    public static async Task<ODataTestApp> CreateAsync(
        string connectionString,
        Action<ODataEntitySetBuilder<Invoice>>? configureEntitySet,
        int? rateLimitPermitLimit,
        Action<ODataExposureOptions>? configureOptions = null,
        Func<string, bool>? permissionPredicate = null)
    {
        // Development: single-instance TestServer — the rate-limiting in-memory
        // counter-store startup guard must not trip on it.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();

        SqlCaptureSink sqlCapture = new();

        builder.Services.AddSingleton<ICurrentTenant, CurrentTenant>();
        // C3b — TenantPartitionedRateLimiter constructor depends on
        // ICurrentUserService (for the per-user partition fallback) and on
        // TimeProvider (for the sliding-window counter). The OData suites
        // only exercise tenant + IP partitioning, so SystemCurrentUserService
        // and the stock wall-clock are the right neutral defaults.
        builder.Services.AddSingleton<ICurrentUserService, SystemCurrentUserService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IPermissionChecker>(new StubPermissionChecker(permissionPredicate));
        builder.Services.AddSingleton(sqlCapture);

        // #3005 — header-driven test scheme; without X-Test-User the request
        // is anonymous, so RequireAuthorization() on the gated metadata group
        // challenges with 401 while AllowAnonymous routes stay reachable.
        builder.Services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, configureOptions: null);
        builder.Services.AddAuthorizationBuilder();

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

        // ADR-050 plumbing: EntityDefinition gate + Export field source.
        // Singleton registrations match the production patterns used by
        // AddEntityDefinition / AddExportDefinition. #3005 adds the
        // Customer / Address exports — every type reachable through a
        // whitelisted $expand path must carry one (startup gate).
        builder.Services.AddSingleton<IEntityDefinitionDescriptor>(new InvoiceEntityDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new InvoiceExportDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new CustomerExportDefinition());
        builder.Services.AddSingleton<IExportDefinitionDescriptor>(new AddressExportDefinition());

        builder.Services.AddGranitODataExposure();

        // C3b — every OData route is gated by the "granit-odata" rate-limit
        // policy. Tests register an in-memory limiter (no Redis) with a
        // generous default cap; suites that exercise 429 explicitly pass a
        // tight limit via the rateLimitPermitLimit parameter.
        builder.Services.AddGranitRateLimiting(options =>
        {
            options.Enabled = true;
            options.Policies["granit-odata"] = new RateLimitPolicyOptions
            {
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                PartitionBy = RateLimitPartition.Tenant,
                PermitLimit = rateLimitPermitLimit ?? 100_000,
                Window = TimeSpan.FromMinutes(1),
            };
        });

        WebApplication app = builder.Build();

        // Tenant-switching middleware — read X-Test-Tenant header, scope the
        // change to the request's lifetime via using/Dispose so AsyncLocal
        // restores cleanly even if downstream throws. ALWAYS open a scope:
        // when no header is present, scope to a null tenant so the static
        // AsyncLocal in CurrentTenant cannot leak a value from a prior test
        // through the same xUnit execution context (regression fix —
        // anonymous request was returning the previous test's tenant rows).
        app.Use(async (context, next) =>
        {
            Guid? tenantId = null;
            if (context.Request.Headers.TryGetValue("X-Test-Tenant", out Microsoft.Extensions.Primitives.StringValues header)
                && Guid.TryParse(header.ToString(), out Guid parsed))
            {
                tenantId = parsed;
            }

            ICurrentTenant currentTenant = context.RequestServices.GetRequiredService<ICurrentTenant>();
            using IDisposable tenantScope = currentTenant.Change(
                tenantId,
                name: tenantId is { } v ? $"Tenant-{v}" : null);
            await next(context).ConfigureAwait(false);
        });

        app.MapGranitODataEndpoints("/api/granit/odata", opts =>
        {
            // Strict-config validator (C6 #1395, #3005) requires every
            // EntitySet to declare its permission and $expand intents, and
            // the mount to declare its $metadata stance. The integration
            // suites are about tenant isolation / query hardening / rate
            // limiting, not auth — apply safe defaults that the per-test
            // callbacks can override (configureEntitySet for the Invoices
            // set; configureOptions for the stance or extra sets).
            opts.AllowAnonymousMetadata();

            ODataEntitySetBuilder<Invoice> builder =
                opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
                    .AllowAnonymousAccess()
                    .DisableExpand();
            configureEntitySet?.Invoke(builder);
            configureOptions?.Invoke(opts);
        });

        await app.StartAsync().ConfigureAwait(false);

        // Apply schema once per host (xUnit fixture lifetime). The multi-tenant
        // filter is parameterised, so the model build no longer captures a tenant
        // value — this can safely run outside any tenant scope.
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
