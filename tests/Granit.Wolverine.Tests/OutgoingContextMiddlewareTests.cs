// =============================================================================
// Tests - OutgoingContextMiddleware
// =============================================================================
// Verifies that X-Tenant-Id, X-User-Id, and traceparent headers are correctly
// injected into outgoing Wolverine envelopes according to the current context.
// =============================================================================

using System.Diagnostics;
using Granit.MultiTenancy;
using Granit.Users;
using Granit.Wolverine.Middleware;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class OutgoingContextMiddlewareTests : IDisposable
{
    private static readonly ActivitySource TestSource = new("test-source");
    private readonly ActivityListener _listener;

    public OutgoingContextMiddlewareTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private static Envelope CreateEnvelope() => new();

    private static (ICurrentTenant, ICurrentUserService) CreateNullContext()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);
        return (tenant, userService);
    }

    // -------------------------------------------------------------------------
    // Tenant + User headers (existing behaviour)
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithTenantAndUser_SetsBothHeaders()
    {
        var tenantId = Guid.NewGuid();
        const string userId = "user-123";

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(tenantId);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns(userId);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader].ShouldBe(tenantId.ToString());
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader].ShouldBe(userId);
    }

    [Fact]
    public void Before_WithTenantOnly_SetsTenantHeaderOnly()
    {
        var tenantId = Guid.NewGuid();

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(tenantId);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader].ShouldBe(tenantId.ToString());
        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserIdHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithUserOnly_SetsUserHeaderOnly()
    {
        const string userId = "user-456";

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns(userId);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.TenantIdHeader).ShouldBeFalse();
        envelope.Headers[OutgoingContextMiddleware.UserIdHeader].ShouldBe(userId);
    }

    [Fact]
    public void Before_WithNoTenantAndNoUser_SetsNoHeaders()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.TenantIdHeader).ShouldBeFalse();
        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserIdHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithAuthenticatedUserButNullUserId_DoesNotSetUserHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns((string?)null);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserIdHeader).ShouldBeFalse();
    }

    [Fact]
    public void Before_WithAuthenticatedUserButEmptyUserId_DoesNotSetUserHeader()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(true);
        userService.UserId.Returns(string.Empty);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.UserIdHeader).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Native Wolverine tenant slot (mirrored in addition to X-Tenant-Id)
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithTenant_SetsNativeEnvelopeTenantId()
    {
        var tenantId = Guid.NewGuid();

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(tenantId);

        ICurrentUserService userService = Substitute.For<ICurrentUserService>();
        userService.IsAuthenticated.Returns(false);

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.TenantId.ShouldBe(tenantId.ToString());
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader].ShouldBe(tenantId.ToString());
    }

    [Fact]
    public void Before_WithNoTenant_DoesNotSetNativeEnvelopeTenantId()
    {
        (ICurrentTenant tenant, ICurrentUserService userService) = CreateNullContext();

        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.TenantId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // traceparent header (W3C Trace Context propagation)
    // -------------------------------------------------------------------------

    [Fact]
    public void Before_WithActiveActivity_SetsTraceParentHeader()
    {
        (ICurrentTenant tenant, ICurrentUserService userService) = CreateNullContext();
        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        using Activity activity = TestSource.StartActivity("http.request")!;

        middleware.Before(envelope);

        envelope.Headers[OutgoingContextMiddleware.TraceParentHeader]
            .ShouldBe(activity.Id);
    }

    [Fact]
    public void Before_WithActiveActivity_TraceParentMatchesW3CFormat()
    {
        (ICurrentTenant tenant, ICurrentUserService userService) = CreateNullContext();
        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        using Activity _ = TestSource.StartActivity("http.request")!;

        middleware.Before(envelope);

        string? traceParent = envelope.Headers[OutgoingContextMiddleware.TraceParentHeader];
        // W3C format: 00-{32 hex}-{16 hex}-{2 hex}
        traceParent!.ShouldMatch("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$");
    }

    [Fact]
    public void Before_WithoutActiveActivity_DoesNotSetTraceParentHeader()
    {
        // Ensure no ambient activity is active for this test.
        Activity.Current = null;

        (ICurrentTenant tenant, ICurrentUserService userService) = CreateNullContext();
        OutgoingContextMiddleware middleware = new(tenant, userService);
        Envelope envelope = CreateEnvelope();

        middleware.Before(envelope);

        envelope.Headers.ContainsKey(OutgoingContextMiddleware.TraceParentHeader).ShouldBeFalse();
    }
}
