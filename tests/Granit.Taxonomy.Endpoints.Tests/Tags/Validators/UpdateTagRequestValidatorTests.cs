using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Tags.Dtos;
using Granit.Taxonomy.Endpoints.Tags.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Tags.Validators;

public sealed class UpdateTagRequestValidatorTests
{
    private readonly UpdateTagRequestValidator _sut = new();

    [Fact]
    public void EmptyRequest_Passes_NoOpHandledByEndpoint()
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest(null, null, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void OnlyName_Passes()
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest("renamed", null, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest("", null, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        string tooLong = new('x', 51);
        ValidationResult result = _sut.Validate(new UpdateTagRequest(tooLong, null, null));
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("FF0000")]
    [InlineData("#GG0000")]
    public void InvalidColor_Fails(string color)
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest(null, color, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidColor_Passes()
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest(null, "#00FF00", null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void OnlyHideOnEntityCardFlag_Passes()
    {
        ValidationResult result = _sut.Validate(new UpdateTagRequest(null, null, true));
        result.IsValid.ShouldBeTrue();
    }
}
