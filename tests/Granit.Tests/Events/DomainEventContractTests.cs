// =============================================================================
// Tests - IDomainEvent / IIntegrationEvent contracts
// =============================================================================
// Verifies that both marker interfaces declare no members, ensuring they remain
// pure markers and do not impose any implementation burden on consumers.
// =============================================================================

using System.Reflection;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Tests.Events;

public sealed class DomainEventContractTests
{
    [Fact]
    public void IDomainEvent_HasNoMembers() =>
        typeof(IDomainEvent).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldBeEmpty("IDomainEvent must be a pure marker interface");

    [Fact]
    public void IIntegrationEvent_HasNoMembers() =>
        typeof(IIntegrationEvent).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldBeEmpty("IIntegrationEvent must be a pure marker interface");

    [Fact]
    public void IDomainEvent_CanBeImplementedByRecord()
    {
        ConcreteDomainEvent evt = new(Guid.NewGuid());

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void IIntegrationEvent_CanBeImplementedByRecord()
    {
        ConcreteIntegrationEvent evt = new(Guid.NewGuid(), "bed-42");

        evt.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void IDomainEvent_AndIIntegrationEvent_AreInCorrectNamespace()
    {
        typeof(IDomainEvent).Namespace.ShouldBe("Granit.Events");
        typeof(IIntegrationEvent).Namespace.ShouldBe("Granit.Events");
    }

    // --- Test fixtures ---

    private sealed record ConcreteDomainEvent(Guid PatientId) : IDomainEvent;

    private sealed record ConcreteIntegrationEvent(Guid PatientId, string BedCode) : IIntegrationEvent;
}
