using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Tests.Events;

public sealed class NullDomainEventDispatcherTests
{
    private readonly NullDomainEventDispatcher _dispatcher = new();

    [Fact]
    public async Task DispatchAsync_ShouldComplete_WithoutThrowing()
    {
        List<IDomainEvent> events = [];

        Func<Task> act = () => _dispatcher.DispatchAsync(events, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DispatchAsync_WithNonEmptyList_ShouldComplete_WithoutThrowing()
    {
        List<IDomainEvent> events = [new FakeDomainEvent()];

        Func<Task> act = () => _dispatcher.DispatchAsync(events, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DispatchAsync_ReturnsCompletedTask()
    {
        List<IDomainEvent> events = [];

        Task result = _dispatcher.DispatchAsync(events, TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        await result;
    }

    [Fact]
    public void ImplementsIDomainEventDispatcher() =>
        _dispatcher.ShouldBeAssignableTo<IDomainEventDispatcher>();

    private sealed record FakeDomainEvent : IDomainEvent;
}
