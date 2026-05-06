using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Tags.Dtos;
using Granit.Taxonomy.Endpoints.Tags.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Tags.Validators;

public sealed class CreateTagRequestValidatorTests
{
    private readonly CreateTagRequestValidator _sut = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        ValidationResult result = _sut.Validate(new CreateTagRequest("documents", "urgent", "#FF0000", null));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyScope_Fails(string scope)
    {
        ValidationResult result = _sut.Validate(new CreateTagRequest(scope, "x", "#000000", null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Scope");
    }

    [Fact]
    public void EmptyName_Fails()
    {
        ValidationResult result = _sut.Validate(new CreateTagRequest("documents", "", "#000000", null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        string tooLong = new('x', 51);
        ValidationResult result = _sut.Validate(new CreateTagRequest("documents", tooLong, "#000000", null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData("FF0000")]
    [InlineData("#GG0000")]
    [InlineData("#FF00")]
    [InlineData("#FF000000")]
    public void InvalidColor_Fails(string color)
    {
        ValidationResult result = _sut.Validate(new CreateTagRequest("documents", "x", color, null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Color");
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#abcdef")]
    [InlineData("#1A2b3C")]
    public void ValidHexColor_Passes(string color)
    {
        ValidationResult result = _sut.Validate(new CreateTagRequest("documents", "x", color, null));
        result.IsValid.ShouldBeTrue();
    }
}
