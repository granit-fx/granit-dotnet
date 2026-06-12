using Granit.Identity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class NullUserSessionProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAsync_ReturnsEmpty()
    {
        NullUserSessionProvider sut = new();

        IReadOnlyList<UserSessionDescriptor> result = await sut.ListAsync("user-1", "session-1", Ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalse() =>
        (await new NullUserSessionProvider().RevokeAsync("user-1", "session-1", Ct)).ShouldBeFalse();

    [Fact]
    public async Task RevokeOthersAsync_ReturnsZero() =>
        (await new NullUserSessionProvider().RevokeOthersAsync("user-1", "session-1", Ct)).ShouldBe(0);

    [Fact]
    public async Task DeviceProvider_ListAsync_ReturnsEmpty() =>
        (await new NullUserDeviceProvider().ListAsync("user-1", Ct)).ShouldBeEmpty();
}
