using Granit.BackgroundJobs.Wolverine.Internal;
using JasperFx.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;
using Xunit;

namespace Granit.BackgroundJobs.Wolverine.Tests;

public sealed class WolverineDeadLetterQueueInspectorTests
{
    [Fact]
    public async Task GetCountsAsync_NullMessageStore_ReturnsEmptyDictionary()
    {
        WolverineDeadLetterQueueInspector sut = new(
            NullLogger<WolverineDeadLetterQueueInspector>.Instance,
            messageStore: null);

        IReadOnlyDictionary<string, long> result = await sut.GetCountsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCountsAsync_MessageStoreThrows_ReturnsEmptyDictionary()
    {
        IMessageStore store = Substitute.For<IMessageStore>();
        IDeadLetters deadLetters = Substitute.For<IDeadLetters>();
        store.DeadLetters.Returns(deadLetters);
        deadLetters
            .SummarizeAllAsync(Arg.Any<string>(), Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        WolverineDeadLetterQueueInspector sut = new(
            NullLogger<WolverineDeadLetterQueueInspector>.Instance,
            store);

        IReadOnlyDictionary<string, long> result = await sut.GetCountsAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCountsAsync_MessageStoreReturnsData_ReturnsMappedCounts()
    {
        IMessageStore store = Substitute.For<IMessageStore>();
        IDeadLetters deadLetters = Substitute.For<IDeadLetters>();
        store.DeadLetters.Returns(deadLetters);

        DeadLetterQueueCount[] counts =
        [
            new("svc", new Uri("tcp://localhost"), "MyCommand", "System.Exception", new Uri("tcp://localhost"), 3),
            new("svc", new Uri("tcp://localhost"), "OtherEvent", "System.Exception", new Uri("tcp://localhost"), 7),
        ];
        deadLetters
            .SummarizeAllAsync(Arg.Any<string>(), Arg.Any<TimeRange>(), Arg.Any<CancellationToken>())
            .Returns(counts);

        WolverineDeadLetterQueueInspector sut = new(
            NullLogger<WolverineDeadLetterQueueInspector>.Instance,
            store);

        IReadOnlyDictionary<string, long> result = await sut.GetCountsAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result["MyCommand"].ShouldBe(3L);
        result["OtherEvent"].ShouldBe(7L);
    }
}
