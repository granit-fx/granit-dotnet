using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Categories.Dtos;
using Granit.Taxonomy.Endpoints.Categories.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Categories.Validators;

public sealed class UpdateCategoryRequestValidatorTests
{
    private readonly UpdateCategoryRequestValidator _sut = new();

    [Fact]
    public void EmptyRequest_Passes_NoOpHandledByEndpoint()
    {
        ValidationResult result = _sut.Validate(new UpdateCategoryRequest(null, null, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void OnlyName_Passes()
    {
        ValidationResult result = _sut.Validate(new UpdateCategoryRequest("hardware", null, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void NameWithSlash_Fails()
    {
        ValidationResult result = _sut.Validate(new UpdateCategoryRequest("a/b", null, null));
        result.IsValid.ShouldBeFalse();
    }
}
