using Granit.Metering.BackgroundJobs.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Metering.BackgroundJobs.Tests.Services;

public sealed class MeteringAggregationScannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IAggregationRunner _aggregationRunner = Substitute.For<IAggregationRunner>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly MeteringAggregationScanner _sut;

    public MeteringAggregationScannerTests()
    {
        _clock.Now.Returns(Now);
        _sut = new MeteringAggregationScanner(
            _aggregationRunner,
            _clock,
            NullLogger<MeteringAggregationScanner>.Instance);
    }

    // ======== Delegates to runner ========

    [Fact]
    public async Task RunAsync_ShouldDelegateToAggregationRunner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await _sut.RunAsync(ct);

        await _aggregationRunner.Received(1).RunAsync(ct);
    }

    // ======== Propagates cancellation ========

    [Fact]
    public async Task RunAsync_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _sut.RunAsync(cts.Token);

        await _aggregationRunner.Received(1).RunAsync(cts.Token);
    }

    // ======== Runner exception propagates ========

    [Fact]
    public async Task RunAsync_RunnerThrows_ShouldPropagate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _aggregationRunner.RunAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Aggregation failed")));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RunAsync(ct));

        exception.Message.ShouldBe("Aggregation failed");
    }
}
