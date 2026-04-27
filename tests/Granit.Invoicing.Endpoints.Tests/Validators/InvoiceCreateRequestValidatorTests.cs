using FluentValidation.TestHelper;
using Granit.Invoicing.Domain;
using Granit.Invoicing.Endpoints.Dtos;
using Granit.Invoicing.Endpoints.Validators;
using Xunit;

namespace Granit.Invoicing.Endpoints.Tests.Validators;

public sealed class InvoiceCreateRequestValidatorTests
{
    private readonly InvoiceCreateRequestValidator _sut = new();

    private static InvoiceCreateRequest ValidRequest(Guid? contactId = null) => new(
        PartyId: contactId ?? Guid.NewGuid(),
        DocumentType: InvoiceDocumentType.Invoice,
        Currency: "EUR",
        CollectionMethod: CollectionMethod.Auto,
        BillingReason: BillingReason.SubscriptionCycle);

    [Fact]
    public void HappyPath_NoErrors() =>
        _sut.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void PartyId_Empty_Fails() =>
        _sut.TestValidate(ValidRequest(Guid.Empty))
            .ShouldHaveValidationErrorFor(r => r.PartyId);

    [Fact]
    public void Currency_Empty_Fails() =>
        _sut.TestValidate(ValidRequest() with { Currency = "" })
            .ShouldHaveValidationErrorFor(r => r.Currency);

    [Fact]
    public void Currency_TooLong_Fails() =>
        _sut.TestValidate(ValidRequest() with { Currency = "EURO" })
            .ShouldHaveValidationErrorFor(r => r.Currency);

    [Fact]
    public void PeriodEnd_BeforeStart_Fails()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset end = start.AddDays(-1);

        _sut.TestValidate(ValidRequest() with { PeriodStart = start, PeriodEnd = end })
            .ShouldHaveValidationErrorFor(r => r.PeriodEnd);
    }
}
