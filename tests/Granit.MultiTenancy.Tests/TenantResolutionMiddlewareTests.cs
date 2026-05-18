// =============================================================================
// TenantResolutionMiddlewareTests - Unit tests for the tenant resolution middleware
// =============================================================================

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.MultiTenancy.Authorization;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Internal;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private static MultiTenancyMetrics CreateMetrics()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(callInfo => new Meter(callInfo.Arg<MeterOptions>().Name));
        return new MultiTenancyMetrics(meterFactory);
    }

    private static TenantResolutionMiddleware CreateMiddleware(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline,
        bool isEnabled = true,
        TenantHeaderTrustMode headerTrustMode = TenantHeaderTrustMode.Unrestricted,
        bool validateTenantExistence = false,
        IHostImpersonationGate? hostImpersonationGate = null)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            IsEnabled = isEnabled,
            HeaderTrustMode = headerTrustMode,
            ValidateTenantExistence = validateTenantExistence,
        });
        ITenantReader tenantReader = Substitute.For<ITenantReader>();
        tenantReader.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Default gate for tests that don't care: allow everything. The dedicated
        // host-impersonation tests below pass their own substitute.
        IHostImpersonationGate gate = hostImpersonationGate ?? AllowAllGate();

        return new TenantResolutionMiddleware(
            currentTenant,
            pipeline,
            tenantReader,
            gate,
            Substitute.For<IHostImpersonationAuditWriter>(),
            CreateMetrics(),
            new TestLocalizer(),
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);
    }

    private static IHostImpersonationGate AllowAllGate()
    {
        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(HostImpersonationDecision.Allow));
#pragma warning restore CA2012
        return gate;
    }

    private static TenantResolverPipeline PipelineReturning(TenantInfo? tenant)
    {
        ITenantResolver resolver = Substitute.For<ITenantResolver>();
        resolver.Order.Returns(100);
        resolver.ResolveAsync(Arg.Any<HttpContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(tenant));
        return new TenantResolverPipeline([resolver]);
    }

    [Fact]
    public async Task Disabled_Skips_Resolution_And_Calls_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(Guid.NewGuid()));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline, isEnabled: false);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task TenantResolved_Calls_Change_And_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        var tenantId = Guid.NewGuid();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(tenantId, "Acme"));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.Received(1).Change(tenantId, "Acme");
    }

    [Fact]
    public async Task TenantResolved_Disposes_Scope_After_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(Guid.NewGuid()));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        scope.Received(1).Dispose();
    }

    [Fact]
    public async Task NoTenantResolved_DoesNotCall_Change()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(null);
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task NoTenantResolved_Still_Calls_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(null);
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
    }

    // ── Phantom tenant validation ────────────────────────────────────

    [Fact]
    public async Task ValidateTenantExistence_Enabled_ExistingTenant_Passes()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(tenantId));
        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline, validateTenantExistence: true);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        currentTenant.Received(1).Change(tenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task ValidateTenantExistence_Enabled_PhantomTenant_Returns403()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var phantomId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(phantomId));
        TenantResolutionMiddleware middleware = CreateMiddlewareWithPhantomRejection(
            currentTenant, pipeline);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ValidateTenantExistence_Disabled_PhantomTenant_Passes()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var phantomId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(phantomId));
        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline, validateTenantExistence: false);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        currentTenant.Received(1).Change(phantomId, Arg.Any<string?>());
    }

    private static TenantResolutionMiddleware CreateMiddlewareWithPhantomRejection(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            ValidateTenantExistence = true,
        });
        ITenantReader tenantReader = Substitute.For<ITenantReader>();
        tenantReader.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false)); // Tenant does NOT exist
        return new TenantResolutionMiddleware(
            currentTenant,
            pipeline,
            tenantReader,
            AllowAllGate(),
            Substitute.For<IHostImpersonationAuditWriter>(),
            CreateMetrics(),
            new TestLocalizer(),
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);
    }

    // ── Host impersonation gate ─────────────────────────────────────

    private static TenantResolverPipeline PipelineWithResolver(string resolverTypeName, TenantInfo? tenant)
    {
        // The middleware identifies the resolver via `GetType().Name`. Use concrete
        // fakes whose simple name matches the production resolvers verbatim.
        ITenantResolver resolver = resolverTypeName switch
        {
            nameof(HeaderTenantResolver) => new HeaderTenantResolverFake(tenant),
            nameof(JwtClaimTenantResolver) => new JwtClaimTenantResolverFake(tenant),
            _ => throw new ArgumentException($"Unknown resolver type {resolverTypeName}"),
        };
        return new TenantResolverPipeline([resolver]);
    }

    private sealed class HeaderTenantResolverFake(TenantInfo? tenant) : ITenantResolver
    {
        public int Order => 100;

        public bool IsAuthoritative => false;

        public Task<TenantInfo?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(tenant);
    }

    private sealed class JwtClaimTenantResolverFake(TenantInfo? tenant) : ITenantResolver
    {
        public int Order => 200;

        public bool IsAuthoritative => true;

        public Task<TenantInfo?> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(tenant);
    }

    private static DefaultHttpContext AuthenticatedContext(string? tenantIdClaim = null)
    {
        DefaultHttpContext context = new();
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, "user-1")];
        if (tenantIdClaim is not null)
        {
            claims.Add(new Claim("tenant_id", tenantIdClaim));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        return context;
    }

    [Fact]
    public async Task HostUser_HeaderResolution_GateDenies_Returns403()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var targetTenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(targetTenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new HostImpersonationDecision(false, "test.denied")));
#pragma warning restore CA2012

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
        await gate.Received(1).CanImpersonateAsync(
            Arg.Any<ClaimsPrincipal>(), targetTenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HostUser_HeaderResolution_GateAllows_Returns200()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var targetTenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(targetTenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012 // NSubstitute Returns(...) idiom for ValueTask-returning methods
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(HostImpersonationDecision.Allow));
#pragma warning restore CA2012

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldNotBe(StatusCodes.Status403Forbidden);
        currentTenant.Received(1).Change(targetTenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task HostUser_JwtResolverResolution_GateNotInvoked()
    {
        // JWT-only resolution is authoritative — no impersonation is taking place.
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(JwtClaimTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        // Host user — no tenant_id claim. JwtClaimTenantResolver wouldn't produce a tenant
        // for a real host principal, but the test substitute does; we verify that even with
        // a JWT resolver producing a tenant, the gate is not consulted.
        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await gate.DidNotReceive().CanImpersonateAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantUser_HeaderMatchesClaim_GateNotInvoked()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: tenantId.ToString());

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await gate.DidNotReceive().CanImpersonateAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        currentTenant.Received(1).Change(tenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task TenantUser_HeaderMismatchesClaim_Returns403_GateNotInvoked()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var headerTenantId = Guid.NewGuid();
        var claimTenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(headerTenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: claimTenantId.ToString());

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        await gate.DidNotReceive().CanImpersonateAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnauthenticatedRequest_HostGateNotInvoked()
    {
        // The gate is only consulted for authenticated principals with no tenant_id claim.
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = new(); // unauthenticated

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await gate.DidNotReceive().CanImpersonateAsync(
            Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── ProblemDetails body + audit hooks ────────────────────────────

    [Fact]
    public async Task HostUser_HeaderResolution_GateDenies_WritesProblemDetailsBody()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var targetTenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(targetTenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new HostImpersonationDecision(false, "HostImpersonation.PermissionDenied")));
#pragma warning restore CA2012

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate,
            hostImpersonationGate: gate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);
        var responseStream = new System.IO.MemoryStream();
        context.Response.Body = responseStream;

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.ContentType.ShouldBe("application/problem+json");

        responseStream.Position = 0;
        using var doc = System.Text.Json.JsonDocument.Parse(responseStream);
        doc.RootElement.GetProperty("type").GetString()
            .ShouldBe("https://granit-fx.dev/errors/host-impersonation-denied");
        doc.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        doc.RootElement.GetProperty("denyReasonCode").GetString()
            .ShouldBe("HostImpersonation.PermissionDenied");
        doc.RootElement.GetProperty("tenantId").GetGuid().ShouldBe(targetTenantId);
        doc.RootElement.GetProperty("title").GetString().ShouldBe("Problem:HostImpersonation.Title");
        doc.RootElement.GetProperty("detail").GetString()
            .ShouldBe("Problem:HostImpersonation.PermissionDenied.Detail");
    }

    [Fact]
    public async Task TenantUser_HeaderMismatchesClaim_WritesProblemDetailsBody()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var headerTenantId = Guid.NewGuid();
        var claimTenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(headerTenantId));

        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline,
            headerTrustMode: TenantHeaderTrustMode.CrossValidate);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: claimTenantId.ToString());
        var responseStream = new System.IO.MemoryStream();
        context.Response.Body = responseStream;

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.ContentType.ShouldBe("application/problem+json");

        responseStream.Position = 0;
        using var doc = System.Text.Json.JsonDocument.Parse(responseStream);
        doc.RootElement.GetProperty("type").GetString()
            .ShouldBe("https://granit-fx.dev/errors/tenant-context-mismatch");
        doc.RootElement.GetProperty("resolvedTenantId").GetGuid().ShouldBe(headerTenantId);
        doc.RootElement.GetProperty("claimTenantId").GetGuid().ShouldBe(claimTenantId);
    }

    [Fact]
    public async Task HostImpersonation_AuditWriter_InvokedForAllowedDecision()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(HostImpersonationDecision.Allow));
#pragma warning restore CA2012

        IHostImpersonationAuditWriter audit = Substitute.For<IHostImpersonationAuditWriter>();

        TenantResolutionMiddleware middleware = CreateMiddlewareWithAudit(
            currentTenant, pipeline, gate, audit);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await audit.Received(1).WriteAsync(
            Arg.Any<ClaimsPrincipal>(),
            tenantId,
            Arg.Is<HostImpersonationDecision>(d => d.Allowed),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HostImpersonation_AuditWriter_InvokedForDeniedDecision()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new HostImpersonationDecision(false, "HostImpersonation.PermissionDenied")));
#pragma warning restore CA2012

        IHostImpersonationAuditWriter audit = Substitute.For<IHostImpersonationAuditWriter>();

        TenantResolutionMiddleware middleware = CreateMiddlewareWithAudit(
            currentTenant, pipeline, gate, audit);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);
        context.Response.Body = new System.IO.MemoryStream();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        await audit.Received(1).WriteAsync(
            Arg.Any<ClaimsPrincipal>(),
            tenantId,
            Arg.Is<HostImpersonationDecision>(d => !d.Allowed && d.DenyReasonCode == "HostImpersonation.PermissionDenied"),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HostImpersonation_AuditWriterThrows_DoesNotBreakRequest()
    {
        // Audit failure must NOT propagate. Request still proceeds (gate said allow).
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineWithResolver(
            nameof(HeaderTenantResolver), new TenantInfo(tenantId));

        IHostImpersonationGate gate = Substitute.For<IHostImpersonationGate>();
#pragma warning disable CA2012
        gate.CanImpersonateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(HostImpersonationDecision.Allow));
#pragma warning restore CA2012

        IHostImpersonationAuditWriter audit = Substitute.For<IHostImpersonationAuditWriter>();
#pragma warning disable CA2012
        audit.WriteAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<Guid>(), Arg.Any<HostImpersonationDecision>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("audit sink down"));
#pragma warning restore CA2012

        TenantResolutionMiddleware middleware = CreateMiddlewareWithAudit(
            currentTenant, pipeline, gate, audit);

        DefaultHttpContext context = AuthenticatedContext(tenantIdClaim: null);
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.Received(1).Change(tenantId, Arg.Any<string?>());
    }

    private static TenantResolutionMiddleware CreateMiddlewareWithAudit(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline,
        IHostImpersonationGate gate,
        IHostImpersonationAuditWriter audit)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            HeaderTrustMode = TenantHeaderTrustMode.CrossValidate,
            ValidateTenantExistence = false,
        });
        ITenantReader tenantReader = Substitute.For<ITenantReader>();
        tenantReader.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        return new TenantResolutionMiddleware(
            currentTenant,
            pipeline,
            tenantReader,
            gate,
            audit,
            CreateMetrics(),
            new TestLocalizer(),
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);
    }

    /// <summary>
    /// Localizer test double — echoes the key as the value so middleware tests can
    /// assert the right key was requested without depending on the JSON resource.
    /// </summary>
    private sealed class TestLocalizer : IStringLocalizer<MultiTenancyLocalizationResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
