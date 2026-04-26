using Granit.Contacts.Domain.ValueObjects;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Endpoints.Tests.Dtos;

public sealed class InvoiceResponseTests
{
    [Fact]
    public void FromEntity_ProjectsContactId()
    {
        var contactId = Guid.NewGuid();
        var invoice = Invoice.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            contactId: ContactId.Create(contactId),
            documentType: InvoiceDocumentType.Invoice,
            currency: "EUR",
            collectionMethod: CollectionMethod.Auto,
            billingReason: BillingReason.SubscriptionCycle);

        var response = InvoiceResponse.FromEntity(invoice);

        response.Id.ShouldBe(invoice.Id);
        response.ContactId.ShouldBe(contactId);
        response.Currency.ShouldBe("EUR");
        response.Status.ShouldBe(InvoiceStatus.Draft.ToString());
    }
}
