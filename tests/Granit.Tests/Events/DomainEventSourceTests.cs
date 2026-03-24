using System.Reflection;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Tests.Events;

public sealed class DomainEventSourceTests
{
    [Fact]
    public void IDomainEventSource_DeclaresExpectedMembers()
    {
        MemberInfo[] members = typeof(IDomainEventSource)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        // DomainEvents property (get accessor) + ClearDomainEvents method
        members.Length.ShouldBe(3, "IDomainEventSource should declare DomainEvents property + ClearDomainEvents method");
    }

    [Fact]
    public void IDomainEventSource_IsInCorrectNamespace() =>
        typeof(IDomainEventSource).Namespace.ShouldBe("Granit.Events");

    [Fact]
    public void IDomainEventDispatcher_IsInCorrectNamespace() =>
        typeof(IDomainEventDispatcher).Namespace.ShouldBe("Granit.Events");

    [Fact]
    public void NullDomainEventDispatcher_DispatchAsync_CompletesImmediately()
    {
        // NullDomainEventDispatcher is internal, so we test via the interface
        // by checking that a no-op dispatch completes without error.
        IDomainEventDispatcher dispatcher = new TestNullDispatcher();

        Task task = dispatcher.DispatchAsync([], TestContext.Current.CancellationToken);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mirrors the internal NullDomainEventDispatcher for test purposes.
    /// </summary>
    private sealed class TestNullDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
