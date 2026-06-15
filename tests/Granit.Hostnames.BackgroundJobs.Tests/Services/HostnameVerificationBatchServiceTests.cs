using Granit.Hostnames.BackgroundJobs.Services;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.BackgroundJobs.Tests.Services;

public sealed class HostnameVerificationBatchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IManagedHostnameReader _reader = Substitute.For<IManagedHostnameReader>();
    private readonly IManagedHostnameWriter _writer = Substitute.For<IManagedHostnameWriter>();
    private readonly IHostnameVerifier _verifier = Substitute.For<IHostnameVerifier>();
    private readonly HostnamesOptions _options = new();

    [Fact]
    public async Task ExecuteAsync_does_nothing_when_no_hostnames_are_due()
    {
        Due();

        await BuildSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _verifier.DidNotReceiveWithAnyArgs().VerifyAsync(Arg.Any<ManagedHostname>(), Arg.Any<CancellationToken>());
        await _writer.DidNotReceiveWithAnyArgs().UpdateAsync(Arg.Any<ManagedHostname>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_queries_the_reader_with_the_clock_and_configured_batch_size()
    {
        _options.VerificationBatchSize = 42;
        Due();

        await BuildSut().ExecuteAsync(TestContext.Current.CancellationToken);

        await _reader.Received(1).ListDueForVerificationAsync(Now, 42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_marks_verified_hostnames_active_and_persists_them()
    {
        ManagedHostname hostname = Hostname("acme.com");
        Due(hostname);
        _verifier.VerifyAsync(hostname, Arg.Any<CancellationToken>())
            .Returns(new HostnameVerificationResult(IsVerified: true, Conflicts: []));

        await BuildSut().ExecuteAsync(TestContext.Current.CancellationToken);

        hostname.Status.ShouldBe(HostnameStatus.Active);
        hostname.LastCheckedAt.ShouldBe(Now);
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_marks_failed_hostnames_error_with_backoff_and_persists_them()
    {
        ManagedHostname hostname = Hostname("broken.example");
        Due(hostname);
        _verifier.VerifyAsync(hostname, Arg.Any<CancellationToken>())
            .Returns(new HostnameVerificationResult(IsVerified: false, Conflicts: []));

        await BuildSut().ExecuteAsync(TestContext.Current.CancellationToken);

        hostname.Status.ShouldBe(HostnameStatus.Error);
        hostname.FailedCheckCount.ShouldBe(1);
        hostname.NextCheckAt.ShouldNotBeNull();
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_isolates_a_failing_verifier_so_the_rest_of_the_batch_proceeds()
    {
        ManagedHostname faulted = Hostname("throws.example");
        ManagedHostname healthy = Hostname("ok.example");
        Due(faulted, healthy);
        _verifier.VerifyAsync(faulted, Arg.Any<CancellationToken>())
            .Returns<HostnameVerificationResult>(_ => throw new InvalidOperationException("dns blew up"));
        _verifier.VerifyAsync(healthy, Arg.Any<CancellationToken>())
            .Returns(new HostnameVerificationResult(IsVerified: true, Conflicts: []));

        await BuildSut().ExecuteAsync(TestContext.Current.CancellationToken);

        // The faulted hostname is logged and skipped; the healthy one still gets persisted.
        healthy.Status.ShouldBe(HostnameStatus.Active);
        await _writer.Received(1).UpdateAsync(healthy, Arg.Any<CancellationToken>());
        await _writer.DidNotReceive().UpdateAsync(faulted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_does_not_swallow_cancellation_from_the_verifier()
    {
        ManagedHostname hostname = Hostname("cancel.example");
        Due(hostname);
        _verifier.VerifyAsync(hostname, Arg.Any<CancellationToken>())
            .Returns<HostnameVerificationResult>(_ => throw new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(
            () => BuildSut().ExecuteAsync(TestContext.Current.CancellationToken));

        await _writer.DidNotReceive().UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    private void Due(params ManagedHostname[] hostnames) =>
        _reader.ListDueForVerificationAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ManagedHostname>)hostnames);

    private static ManagedHostname Hostname(string host) =>
        ManagedHostname.Create(Guid.NewGuid(), Domain.Hostname.Create(host), "cms.site", Guid.NewGuid());

    private HostnameVerificationBatchService BuildSut() =>
        new(_reader, _writer, _verifier, new FixedTimeProvider(Now),
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<HostnameVerificationBatchService>.Instance);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
