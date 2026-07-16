using Granit.Identity.Federated.Exceptions;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Internal;

/// <summary>
/// Behaviour cover for the graceful-degradation decorator (Vague 6b): read operations degrade
/// on classified faults, but cancellation and authorization failures are never swallowed, and
/// write operations always pass through.
/// </summary>
public sealed class GracefulIdentityProviderDecoratorTests
{
    private readonly IIdentityProvider _inner = Substitute.For<IIdentityProvider>();

    private GracefulIdentityProviderDecorator Sut() =>
        new(_inner, NullLogger<GracefulIdentityProviderDecorator>.Instance);

    private static IdentityProviderException Fault(IdentityProviderFailureCategory category) => category switch
    {
        IdentityProviderFailureCategory.Unauthorized => new IdentityProviderUnauthorizedException("keycloak", "get_users"),
        IdentityProviderFailureCategory.NotFound => new IdentityProviderNotFoundException("keycloak", "get_users"),
        IdentityProviderFailureCategory.Throttled => new IdentityProviderThrottledException("keycloak", "get_users"),
        _ => new IdentityProviderTransientException("keycloak", "get_users"),
    };

    [Theory]
    [InlineData(IdentityProviderFailureCategory.Transient)]
    [InlineData(IdentityProviderFailureCategory.Throttled)]
    [InlineData(IdentityProviderFailureCategory.NotFound)]
    public async Task Read_degrades_to_empty_on_degradable_fault(IdentityProviderFailureCategory category)
    {
        _inner.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(Fault(category));

        IReadOnlyList<IIdentityUser> result = await Sut().GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_rethrows_unauthorized_rather_than_degrading()
    {
        _inner.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(Fault(IdentityProviderFailureCategory.Unauthorized));

        await Should.ThrowAsync<IdentityProviderUnauthorizedException>(
            () => Sut().GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_never_swallows_cancellation()
    {
        _inner.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => Sut().GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_does_not_swallow_unexpected_exceptions()
    {
        // Only classified provider faults degrade — a genuine bug must surface, not vanish as "no data".
        _inner.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken).ThrowsAsyncForAnyArgs(new InvalidOperationException("bug"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => Sut().GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetUser_degrades_to_null_on_transient()
    {
        _inner.GetUserAsync("u1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderTransientException("keycloak", "get_user"));

        IIdentityUser? result = await Sut().GetUserAsync("u1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Read_returns_inner_result_on_success()
    {
        IReadOnlyList<IIdentityUser> users = [Substitute.For<IIdentityUser>()];
        _inner.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken).ReturnsForAnyArgs(users);

        IReadOnlyList<IIdentityUser> result = await Sut().GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(users);
    }

    [Fact]
    public async Task Write_passes_through_and_never_degrades()
    {
        IdentityUserCreate create = new("u@example.com", "u@example.com", null, null, true, null);
        _inner.CreateUserAsync(create, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderTransientException("keycloak", "create_user"));

        // A failed mutation must surface — the decorator does not degrade writes.
        await Should.ThrowAsync<IdentityProviderTransientException>(
            () => Sut().CreateUserAsync(create, TestContext.Current.CancellationToken));
    }
}
