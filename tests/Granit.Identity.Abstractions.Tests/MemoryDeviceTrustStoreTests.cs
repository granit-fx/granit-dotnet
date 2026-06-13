using Granit.Identity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class MemoryDeviceTrustStoreTests
{
    private static readonly DateTimeOffset TrustedAt = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MemoryDeviceTrustStore _sut = new();

    private static DeviceTrustVerdict Verdict(DateTimeOffset? until = null) =>
        new(DeviceTrustLevel.Remembered, TrustedAt, until, "user_marked");

    [Fact]
    public async Task GetAsync_WhenNoneRecorded_ReturnsNull() =>
        (await _sut.GetAsync("u1", "d1", Ct)).ShouldBeNull();

    [Fact]
    public async Task SetThenGet_RoundTripsTheVerdict()
    {
        await _sut.SetAsync("u1", "d1", Verdict(), Ct);

        DeviceTrustVerdict? read = await _sut.GetAsync("u1", "d1", Ct);

        read.ShouldNotBeNull();
        read.Level.ShouldBe(DeviceTrustLevel.Remembered);
        read.Reason.ShouldBe("user_marked");
    }

    [Fact]
    public async Task SetAsync_Twice_ReplacesTheVerdict()
    {
        await _sut.SetAsync("u1", "d1", Verdict(), Ct);
        await _sut.SetAsync("u1", "d1", new DeviceTrustVerdict(DeviceTrustLevel.Strong, TrustedAt, null, "passkey"), Ct);

        (await _sut.GetAsync("u1", "d1", Ct))!.Level.ShouldBe(DeviceTrustLevel.Strong);
    }

    [Fact]
    public async Task GetManyAsync_ReturnsOnlyRecordedDevices()
    {
        await _sut.SetAsync("u1", "d1", Verdict(), Ct);
        await _sut.SetAsync("u1", "d2", Verdict(), Ct);

        IReadOnlyDictionary<string, DeviceTrustVerdict> result =
            await _sut.GetManyAsync("u1", ["d1", "d2", "d3"], Ct);

        result.Count.ShouldBe(2);
        result.ShouldContainKey("d1");
        result.ShouldContainKey("d2");
        result.ShouldNotContainKey("d3");
    }

    [Fact]
    public async Task RevokeAsync_RemovesTheVerdict()
    {
        await _sut.SetAsync("u1", "d1", Verdict(), Ct);

        await _sut.RevokeAsync("u1", "d1", Ct);

        (await _sut.GetAsync("u1", "d1", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task RevokeAsync_WhenAbsent_IsNoOp() =>
        await Should.NotThrowAsync(() => _sut.RevokeAsync("u1", "missing", Ct));

    [Fact]
    public void IsActive_RespectsLevelAndExpiry()
    {
        DateTimeOffset now = TrustedAt;

        Verdict(until: now.AddDays(1)).IsActive(now).ShouldBeTrue();
        Verdict(until: now.AddSeconds(-1)).IsActive(now).ShouldBeFalse();
        Verdict(until: null).IsActive(now).ShouldBeTrue();
        new DeviceTrustVerdict(DeviceTrustLevel.None, TrustedAt, now.AddDays(1)).IsActive(now).ShouldBeFalse();
    }
}
