// =============================================================================
// Tests - TenantContextBehavior
// =============================================================================
// Verifies that X-Tenant-Id header is correctly restored to ICurrentTenant
// in incoming Wolverine envelopes, and that the scope is disposed on After().
// =============================================================================

using Granit.MultiTenancy;
using Granit.Wolverine.Behaviors;
using Granit.Wolverine.Middleware;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class TenantContextBehaviorTests
{
    [Fact]
    public void Before_WithValidTenantHeader_CallsChange()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(tenantId).Returns(Substitute.For<IDisposable>());

        TenantContextBehavior behavior = new(tenant);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = tenantId.ToString();

        behavior.Before(envelope);

        tenant.Received(1).Change(tenantId);
    }

    [Fact]
    public void Before_WithMissingHeader_DoesNotCallChange()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();

        TenantContextBehavior behavior = new(tenant);
        Envelope envelope = new();

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
    }

    [Fact]
    public void Before_WithInvalidGuidHeader_DoesNotCallChange()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();

        TenantContextBehavior behavior = new(tenant);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = "not-a-guid";

        behavior.Before(envelope);

        tenant.DidNotReceive().Change(Arg.Any<Guid?>());
    }

    [Fact]
    public void After_WhenScopeWasSet_DisposesScope()
    {
        var tenantId = Guid.NewGuid();
        IDisposable scope = Substitute.For<IDisposable>();

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(tenantId).Returns(scope);

        TenantContextBehavior behavior = new(tenant);
        Envelope envelope = new();
        envelope.Headers[OutgoingContextMiddleware.TenantIdHeader] = tenantId.ToString();

        behavior.Before(envelope);
        behavior.After();

        scope.Received(1).Dispose();
    }

    [Fact]
    public void After_WhenNoScopeWasSet_DoesNotThrow()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        TenantContextBehavior behavior = new(tenant);

        Action act = behavior.After;

        Should.NotThrow(act);
    }
}
