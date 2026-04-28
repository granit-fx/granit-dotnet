using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Dtos;
using Granit.Parties.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Endpoints.Tests.Dtos;

public sealed class InvoiceResponseTests
{
    [Fact]
    public void FromEntity_ProjectsPartyId()
    {
        var partyId = Guid.NewGuid();
        var invoice = Invoice.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            partyId: PartyId.Create(partyId),
            documentType: InvoiceDocumentType.Invoice,
            currency: "EUR",
            collectionMethod: CollectionMethod.Auto,
            billingReason: BillingReason.SubscriptionCycle);

        var response = InvoiceResponse.FromEntity(invoice);

        response.Id.ShouldBe(invoice.Id);
        response.PartyId.ShouldBe(partyId);
        response.Currency.ShouldBe("EUR");
        response.Status.ShouldBe(InvoiceStatus.Draft.ToString());
    }
}
