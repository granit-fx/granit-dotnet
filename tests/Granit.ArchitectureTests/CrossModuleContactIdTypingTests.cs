using System.Reflection;
using Granit.Contacts.Domain.ValueObjects;
using Granit.CustomerBalance.Domain;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// US #1245 — pin cross-module consistency: every aggregate that exposes a contact
/// reference uses the typed <see cref="ContactId"/> value object, not a bare
/// <see cref="Guid"/> named "contactId" / "customerId". Reverting any of these to a
/// raw <c>Guid</c> would let downstream callers pass an unrelated identifier
/// (TenantId, UserId, …) without compile-time checks.
/// </summary>
public sealed class CrossModuleContactIdTypingTests
{
    private static readonly Type[] AggregatesCarryingContactId =
    [
        typeof(Invoice),
        typeof(Subscription),
        typeof(BalanceAccount),
    ];

    [Theory]
    [InlineData(typeof(Invoice))]
    [InlineData(typeof(Subscription))]
    [InlineData(typeof(BalanceAccount))]
    public void Aggregate_ContactId_IsTypedValueObject(Type aggregateType)
    {
        PropertyInfo? prop = aggregateType.GetProperty("ContactId");

        prop.ShouldNotBeNull(
            $"{aggregateType.Name} must expose a ContactId property after the Contacts migration.");
        prop.PropertyType.ShouldBe(typeof(ContactId),
            $"{aggregateType.Name}.ContactId must remain the typed ContactId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without compile-time checks.");
    }

    [Fact]
    public void All_5_migrated_aggregates_carry_a_typed_ContactId()
    {
        IEnumerable<string> missing = AggregatesCarryingContactId
            .Where(t => t.GetProperty("ContactId")?.PropertyType != typeof(ContactId))
            .Select(t => t.Name);

        missing.ShouldBeEmpty(
            "Every aggregate listed in CrossModuleContactIdTypingTests must expose a typed ContactId property. " +
            "Add the missing ContactId or remove the type from the registry if the aggregate genuinely does not " +
            "need a billing identity.");
    }

    /// <summary>
    /// US #1245 AC: every Eto event raised by a contact-aware aggregate carries a Guid ContactId.
    /// </summary>
    [Theory]
    [InlineData(typeof(Granit.Invoicing.Events.InvoiceFinalizedEto))]
    [InlineData(typeof(Granit.Invoicing.Events.InvoicePaidEto))]
    [InlineData(typeof(Granit.Invoicing.Events.InvoiceOverdueEto))]
    [InlineData(typeof(Granit.Invoicing.Events.CreditNoteIssuedEto))]
    [InlineData(typeof(Granit.Invoicing.Events.OverpaymentDetectedEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionCreatedEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionActivatedEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionSuspendedEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionCancelledEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionExpiredEto))]
    [InlineData(typeof(Granit.Subscriptions.Events.SubscriptionPlanChangedEto))]
    [InlineData(typeof(Granit.CustomerBalance.Events.BalanceCreditedEto))]
    [InlineData(typeof(Granit.CustomerBalance.Events.BalanceDebitedEto))]
    public void Eto_CarriesGuidContactId(Type etoType)
    {
        PropertyInfo? prop = etoType.GetProperty("ContactId");

        prop.ShouldNotBeNull($"{etoType.Name} must carry a ContactId.");
        prop.PropertyType.ShouldBe(typeof(Guid),
            $"{etoType.Name}.ContactId must be a Guid (Etos serialise across process boundaries — typed VOs " +
            "do not survive JSON deserialisation by every consumer). The aggregate-side property uses the " +
            "typed ContactId value object; the wire-side Eto carries its raw Guid value.");
    }
}
