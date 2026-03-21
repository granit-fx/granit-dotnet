#pragma warning disable CS0618

using Granit.Identity.Events;
using Granit.Identity.Extensions;
using Granit.Identity.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class IdentityEventPublisherTests
{
    private readonly NullIdentityEventPublisher _publisher = new();

    [Fact]
    public void ImplementsIIdentityEventPublisher() =>
        _publisher.ShouldBeAssignableTo<IIdentityEventPublisher>();

    [Fact]
    public async Task PublishAsync_CompletesWithoutError()
    {
        var evt = new IdentityUserCreatedEto("u1", "alice", "alice@test.com");

        await Should.NotThrowAsync(
            () => _publisher.PublishAsync(evt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WithEnabledChangedEvent_CompletesWithoutError()
    {
        var evt = new IdentityUserEnabledChangedEto("u1", true);

        await Should.NotThrowAsync(
            () => _publisher.PublishAsync(evt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WithRoleAssignedEvent_CompletesWithoutError()
    {
        var evt = new IdentityRoleAssignedEto("u1", "admin");

        await Should.NotThrowAsync(
            () => _publisher.PublishAsync(evt, TestContext.Current.CancellationToken));
    }

}
