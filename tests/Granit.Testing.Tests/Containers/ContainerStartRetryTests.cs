using Granit.Testing.Containers;
using Shouldly;

namespace Granit.Testing.Tests.Containers;

public sealed class ContainerStartRetryTests
{
    [Fact]
    public async Task RunWithRetryAsync_FirstAttemptSucceeds_InvokesActionOnce()
    {
        int invocations = 0;

        await ContainerStartRetry.RunWithRetryAsync(
            _ =>
            {
                invocations++;
                return Task.CompletedTask;
            },
            label: "ok-fixture",
            cancellationToken: TestContext.Current.CancellationToken);

        invocations.ShouldBe(1);
    }

    [Fact]
    public async Task RunWithRetryAsync_TransientFailureThenSuccess_RetriesAndCompletes()
    {
        int invocations = 0;

        await ContainerStartRetry.RunWithRetryAsync(
            _ =>
            {
                invocations++;
                if (invocations < 3)
                {
                    throw new InvalidOperationException("pull access denied");
                }

                return Task.CompletedTask;
            },
            label: "transient-fixture",
            maxAttempts: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        invocations.ShouldBe(3);
    }

    [Fact]
    public async Task RunWithRetryAsync_ExhaustsAttempts_PropagatesLastException()
    {
        int invocations = 0;

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ContainerStartRetry.RunWithRetryAsync(
                _ =>
                {
                    invocations++;
                    throw new InvalidOperationException($"attempt {invocations} failed");
                },
                label: "always-fails-fixture",
                maxAttempts: 3,
                cancellationToken: TestContext.Current.CancellationToken));

        invocations.ShouldBe(3);
        ex.Message.ShouldBe("attempt 3 failed");
    }

    [Fact]
    public async Task RunWithRetryAsync_CancellationRequested_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        int invocations = 0;

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await ContainerStartRetry.RunWithRetryAsync(
                _ =>
                {
                    invocations++;
                    throw new OperationCanceledException(cts.Token);
                },
                label: "cancelled-fixture",
                maxAttempts: 3,
                cancellationToken: cts.Token));

        // First invocation throws OperationCanceledException which short-circuits retry.
        invocations.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunWithRetryAsync_InvalidMaxAttempts_Throws(int maxAttempts)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await ContainerStartRetry.RunWithRetryAsync(
                _ => Task.CompletedTask,
                label: "ok",
                maxAttempts: maxAttempts,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunWithRetryAsync_NullLabel_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(async () =>
            await ContainerStartRetry.RunWithRetryAsync(
                _ => Task.CompletedTask,
                label: null!,
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
