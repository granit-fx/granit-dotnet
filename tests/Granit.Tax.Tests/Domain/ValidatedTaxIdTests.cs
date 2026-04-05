using Granit.Tax.Domain;
using Granit.Tax.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tax.Tests.Domain;

public sealed class ValidatedTaxIdTests
{
    // ======== Create ========

    [Fact]
    public void Create_ValidParameters_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        DateTimeOffset validatedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = validatedAt.AddHours(24);
        var companyDetails = new CompanyValidationDetails("Acme Corp", "123 Main St", "REQ-001");

        var taxId = ValidatedTaxId.Create(
            id, "BE0123456789", "BE", isValid: true,
            TaxIdValidationSource.Vies, validatedAt, expiresAt, companyDetails);

        taxId.Id.ShouldBe(id);
        taxId.TaxId.ShouldBe("BE0123456789");
        taxId.CountryCode.ShouldBe("BE");
        taxId.IsValid.ShouldBeTrue();
        taxId.Source.ShouldBe(TaxIdValidationSource.Vies);
        taxId.ValidatedAt.ShouldBe(validatedAt);
        taxId.ExpiresAt.ShouldBe(expiresAt);
        taxId.CompanyName.ShouldBe("Acme Corp");
        taxId.CompanyAddress.ShouldBe("123 Main St");
        taxId.RequestIdentifier.ShouldBe("REQ-001");
    }

    [Fact]
    public void Create_WithoutOptionalParams_ShouldLeaveNulls()
    {
        var taxId = ValidatedTaxId.Create(
            Guid.NewGuid(), "DE123456789", "DE", isValid: false,
            TaxIdValidationSource.OfflinePending, DateTimeOffset.UtcNow);

        taxId.ExpiresAt.ShouldBeNull();
        taxId.CompanyName.ShouldBeNull();
        taxId.CompanyAddress.ShouldBeNull();
        taxId.RequestIdentifier.ShouldBeNull();
    }

    [Fact]
    public void Create_NullTaxId_ShouldThrowArgumentException() =>
        Should.Throw<ArgumentException>(() =>
            ValidatedTaxId.Create(
                Guid.NewGuid(), null!, "BE", true,
                TaxIdValidationSource.Vies, DateTimeOffset.UtcNow));

    [Fact]
    public void Create_EmptyCountryCode_ShouldThrowArgumentException() =>
        Should.Throw<ArgumentException>(() =>
            ValidatedTaxId.Create(
                Guid.NewGuid(), "BE0123456789", "", true,
                TaxIdValidationSource.Vies, DateTimeOffset.UtcNow));

    // ======== ConfirmOnlineValidation ========

    [Fact]
    public void ConfirmOnlineValidation_ShouldUpdateProperties()
    {
        var taxId = ValidatedTaxId.Create(
            Guid.NewGuid(), "FR12345678901", "FR", isValid: false,
            TaxIdValidationSource.OfflinePending, DateTimeOffset.UtcNow);

        DateTimeOffset newValidatedAt = DateTimeOffset.UtcNow.AddMinutes(30);
        var companyDetails = new CompanyValidationDetails("SARL Test", "1 Rue de Paris", "VIES-42");

        taxId.ConfirmOnlineValidation(
            isValid: true, TaxIdValidationSource.Vies, newValidatedAt, companyDetails);

        taxId.IsValid.ShouldBeTrue();
        taxId.Source.ShouldBe(TaxIdValidationSource.Vies);
        taxId.ValidatedAt.ShouldBe(newValidatedAt);
        taxId.CompanyName.ShouldBe("SARL Test");
        taxId.CompanyAddress.ShouldBe("1 Rue de Paris");
        taxId.RequestIdentifier.ShouldBe("VIES-42");
    }

    [Fact]
    public void ConfirmOnlineValidation_WithoutCompanyDetails_ShouldClearPreviousValues()
    {
        var companyDetails = new CompanyValidationDetails("Old Corp", "Old Addr", "OLD-1");
        var taxId = ValidatedTaxId.Create(
            Guid.NewGuid(), "IT12345678901", "IT", isValid: true,
            TaxIdValidationSource.Vies, DateTimeOffset.UtcNow,
            companyDetails: companyDetails);

        taxId.ConfirmOnlineValidation(
            isValid: false, TaxIdValidationSource.Vies, DateTimeOffset.UtcNow);

        taxId.CompanyName.ShouldBeNull();
        taxId.CompanyAddress.ShouldBeNull();
        taxId.RequestIdentifier.ShouldBeNull();
    }
}
