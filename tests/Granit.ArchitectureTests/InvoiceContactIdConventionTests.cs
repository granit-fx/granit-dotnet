using System.Reflection;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Invoicing.Domain;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the contract from US #1232: <c>Invoice.ContactId</c> is the typed
/// <see cref="ContactId"/> value object — never a bare <see cref="Guid"/>. Reverting it
/// would lose compile-time safety and let downstream callers pass arbitrary Guids that
/// happen to be the wrong identifier (TenantId, UserId…).
/// </summary>
public sealed class InvoiceContactIdConventionTests
{
    [Fact]
    public void Invoice_ContactId_IsTypedContactIdValueObject()
    {
        PropertyInfo? prop = typeof(Invoice).GetProperty(nameof(Invoice.ContactId));

        prop.ShouldNotBeNull();
        prop.PropertyType.ShouldBe(typeof(ContactId),
            "Invoice.ContactId must remain the typed ContactId value object — using a bare Guid " +
            "would let callers pass any unrelated identifier (TenantId, UserId, etc.) without " +
            "compile-time checks.");
    }
}
