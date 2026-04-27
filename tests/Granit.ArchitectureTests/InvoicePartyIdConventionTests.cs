using System.Reflection;
using Granit.Invoicing.Domain;
using Granit.Parties.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the contract from US #1232: <c>Invoice.PartyId</c> is the typed
/// <see cref="PartyId"/> value object — never a bare <see cref="Guid"/>. Reverting it
/// would lose compile-time safety and let downstream callers pass arbitrary Guids that
/// happen to be the wrong identifier (TenantId, UserId…).
/// </summary>
public sealed class InvoicePartyIdConventionTests
{
    [Fact]
    public void Invoice_ContactId_IsTypedContactIdValueObject()
    {
        PropertyInfo? prop = typeof(Invoice).GetProperty(nameof(Invoice.PartyId));

        prop.ShouldNotBeNull();
        prop.PropertyType.ShouldBe(typeof(PartyId),
            "Invoice.PartyId must remain the typed PartyId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without " +
            "compile-time checks.");
    }
}
