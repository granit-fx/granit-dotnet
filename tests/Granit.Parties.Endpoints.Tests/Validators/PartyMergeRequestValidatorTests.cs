using FluentValidation.TestHelper;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Validators;

public sealed class PartyMergeRequestValidatorTests
{
    private readonly PartyMergeRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        var request = new PartyMergeRequest(
            LoserId: Guid.NewGuid(),
            Choices: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Name"] = "Survivor",
                ["TaxStatus"] = "Loser",
            },
            Reason: "Doublon créé par sync ERP");

        _validator.TestValidate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Empty_LoserId_Fails() =>
        _validator.TestValidate(new PartyMergeRequest(LoserId: Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.LoserId);

    [Fact]
    public void Reason_Over_1000_Chars_Fails() =>
        _validator.TestValidate(new PartyMergeRequest(
            LoserId: Guid.NewGuid(),
            Reason: new string('x', 1001)))
            .ShouldHaveValidationErrorFor(x => x.Reason);

    [Theory]
    [InlineData("survivor")]   // wrong casing
    [InlineData("LOSER")]
    [InlineData("Both")]
    [InlineData("")]
    public void Invalid_Choice_Value_Fails(string value)
    {
        var request = new PartyMergeRequest(
            LoserId: Guid.NewGuid(),
            Choices: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Name"] = value,
            });

        _validator.TestValidate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Null_Choices_IsAllowed() =>
        _validator.TestValidate(new PartyMergeRequest(LoserId: Guid.NewGuid(), Choices: null))
            .IsValid.ShouldBeTrue();

    [Fact]
    public void Empty_Choices_IsAllowed() =>
        _validator.TestValidate(new PartyMergeRequest(
            LoserId: Guid.NewGuid(),
            Choices: new Dictionary<string, string>(StringComparer.Ordinal)))
            .IsValid.ShouldBeTrue();
}
