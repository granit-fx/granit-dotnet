using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Categories.Dtos;
using Granit.Taxonomy.Endpoints.Categories.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Categories.Validators;

public sealed class CreateCategoryRequestValidatorTests
{
    private readonly CreateCategoryRequestValidator _sut = new();

    [Fact]
    public void Valid_RootCategory_Passes()
    {
        ValidationResult result = _sut.Validate(new CreateCategoryRequest("products", null, "electronics", null, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ChildCategory_Passes()
    {
        ValidationResult result = _sut.Validate(new CreateCategoryRequest("products", Guid.NewGuid(), "laptops", "laptop", true));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyScope_Fails(string scope)
    {
        ValidationResult result = _sut.Validate(new CreateCategoryRequest(scope, null, "x", null, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void NameWithSlash_Fails()
    {
        ValidationResult result = _sut.Validate(new CreateCategoryRequest("products", null, "a/b", null, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void NameTooLong_Fails()
    {
        string tooLong = new('x', 101);
        ValidationResult result = _sut.Validate(new CreateCategoryRequest("products", null, tooLong, null, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IconTooLong_Fails()
    {
        string tooLong = new('x', 101);
        ValidationResult result = _sut.Validate(new CreateCategoryRequest("products", null, "x", tooLong, null));
        result.IsValid.ShouldBeFalse();
    }
}
