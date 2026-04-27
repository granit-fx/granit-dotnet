using System.Reflection;
using Granit.CustomerBalance.Domain;
using Granit.Invoicing.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Subscriptions.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// US #1245 — pin cross-module consistency: every aggregate that exposes a contact
/// reference uses the typed <see cref="PartyId"/> value object, not a bare
/// <see cref="Guid"/> named "contactId" / "customerId". Reverting any of these to a
/// raw <c>Guid</c> would let downstream callers pass an unrelated identifier
/// (TenantId, UserId, …) without compile-time checks.
/// </summary>
public sealed class CrossModulePartyIdTypingTests
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
        PropertyInfo? prop = aggregateType.GetProperty("PartyId");

        prop.ShouldNotBeNull(
            $"{aggregateType.Name} must expose a PartyId property after the Parties migration.");
        prop.PropertyType.ShouldBe(typeof(PartyId),
            $"{aggregateType.Name}.PartyId must remain the typed PartyId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without compile-time checks.");
    }

    [Fact]
    public void All_5_migrated_aggregates_carry_a_typed_ContactId()
    {
        IEnumerable<string> missing = AggregatesCarryingContactId
            .Where(t => t.GetProperty("PartyId")?.PropertyType != typeof(PartyId))
            .Select(t => t.Name);

        missing.ShouldBeEmpty(
            "Every aggregate listed in CrossModulePartyIdTypingTests must expose a typed PartyId property. " +
            "Add the missing PartyId or remove the type from the registry if the aggregate genuinely does not " +
            "need a billing identity.");
    }

    /// <summary>
    /// US #1245 AC: every Eto event raised by a contact-aware aggregate carries a Guid PartyId.
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
        PropertyInfo? prop = etoType.GetProperty("PartyId");

        prop.ShouldNotBeNull($"{etoType.Name} must carry a PartyId.");
        prop.PropertyType.ShouldBe(typeof(Guid),
            $"{etoType.Name}.PartyId must be a Guid (Etos serialise across process boundaries — typed VOs " +
            "do not survive JSON deserialisation by every consumer). The aggregate-side property uses the " +
            "typed PartyId value object; the wire-side Eto carries its raw Guid value.");
    }
}
