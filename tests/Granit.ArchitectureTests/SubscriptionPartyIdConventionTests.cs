using System.Reflection;
using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the contract from US #1236: <c>Subscription.PartyId</c> is the typed
/// <see cref="PartyId"/> value object — never a bare <see cref="Guid"/>. Mirrors
/// <c>InvoicePartyIdConventionTests</c> for the Subscription aggregate so the
/// next module migrations stay consistent.
/// </summary>
public sealed class SubscriptionPartyIdConventionTests
{
    [Fact]
    public void Subscription_PartyId_IsTypedPartyIdValueObject()
    {
        PropertyInfo? prop = typeof(Subscription).GetProperty(nameof(Subscription.PartyId));

        prop.ShouldNotBeNull();
        prop.PropertyType.ShouldBe(typeof(PartyId),
            "Subscription.PartyId must remain the typed PartyId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without compile-time checks.");
    }
}
