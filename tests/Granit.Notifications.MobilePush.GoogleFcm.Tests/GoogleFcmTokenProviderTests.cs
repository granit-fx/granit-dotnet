using Granit.Notifications.MobilePush.GoogleFcm.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.GoogleFcm.Tests;

public sealed class GoogleFcmTokenProviderTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCallers_MintExactlyOnce()
    {
        CountingTokenSource source = new(Anchor.AddHours(1)) { MintDelay = TimeSpan.FromMilliseconds(50) };
        using GoogleFcmTokenProvider provider = new(source, FixedClock(Anchor));

        string[] tokens = await Task.WhenAll(Enumerable.Range(0, 25)
            .Select(_ => provider.GetAccessTokenAsync(TestContext.Current.CancellationToken).AsTask()));

        source.MintCount.ShouldBe(1);
        tokens.ShouldAllBe(t => t == "token-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithinMargin_ReturnsCachedToken()
    {
        CountingTokenSource source = new(Anchor.AddHours(1));
        using GoogleFcmTokenProvider provider = new(source, FixedClock(Anchor));

        string first = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        string second = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        source.MintCount.ShouldBe(1);
        second.ShouldBe(first);
    }

    [Fact]
    public async Task GetAccessTokenAsync_InsideRefreshMargin_MintsFreshToken()
    {
        // Token expires at +1h; the provider refreshes 5 minutes early.
        CountingTokenSource source = new(Anchor.AddHours(1));
        DateTimeOffset now = Anchor;
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_ => now);
        using GoogleFcmTokenProvider provider = new(source, clock);

        await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        now = Anchor.AddMinutes(56); // 4 min before expiry — inside the 5 min margin
        string refreshed = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        source.MintCount.ShouldBe(2);
        refreshed.ShouldBe("token-2");
    }

    [Fact]
    public async Task Invalidate_ForcesMintOnNextCall()
    {
        CountingTokenSource source = new(Anchor.AddHours(1));
        using GoogleFcmTokenProvider provider = new(source, FixedClock(Anchor));

        await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);
        provider.Invalidate();
        string refreshed = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        source.MintCount.ShouldBe(2);
        refreshed.ShouldBe("token-2");
    }

    private static IClock FixedClock(DateTimeOffset now)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(now);
        return clock;
    }

    /// <summary>Thread-safe fake token source: counts mints and numbers the issued tokens.</summary>
    private sealed class CountingTokenSource(DateTimeOffset expiresAt) : IGoogleFcmTokenSource
    {
        private int _mintCount;

        public TimeSpan MintDelay { get; init; }

        public int MintCount => Volatile.Read(ref _mintCount);

        public async Task<GoogleFcmAccessToken> MintTokenAsync(CancellationToken cancellationToken = default)
        {
            int count = Interlocked.Increment(ref _mintCount);
            if (MintDelay > TimeSpan.Zero)
            {
                await Task.Delay(MintDelay, cancellationToken).ConfigureAwait(false);
            }

            return new GoogleFcmAccessToken($"token-{count}", expiresAt);
        }
    }
}
