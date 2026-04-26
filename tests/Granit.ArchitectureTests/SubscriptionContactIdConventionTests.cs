using System.Reflection;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the contract from US #1236: <c>Subscription.ContactId</c> is the typed
/// <see cref="ContactId"/> value object — never a bare <see cref="Guid"/>. Mirrors
/// <c>InvoiceContactIdConventionTests</c> for the Subscription aggregate so the
/// next module migrations stay consistent.
/// </summary>
public sealed class SubscriptionContactIdConventionTests
{
    [Fact]
    public void Subscription_ContactId_IsTypedContactIdValueObject()
    {
        PropertyInfo? prop = typeof(Subscription).GetProperty(nameof(Subscription.ContactId));

        prop.ShouldNotBeNull();
        prop.PropertyType.ShouldBe(typeof(ContactId),
            "Subscription.ContactId must remain the typed ContactId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without compile-time checks.");
    }
}
