using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Categories.Dtos;
using Granit.Taxonomy.Endpoints.Categories.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Categories.Validators;

public sealed class AssignCategoryRequestValidatorTests
{
    private readonly AssignCategoryRequestValidator _sut = new();

    [Fact]
    public void Valid_Passes()
    {
        ValidationResult result = _sut.Validate(new AssignCategoryRequest("Granit.Documents.Domain.Document", Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyTargetType_Fails()
    {
        ValidationResult result = _sut.Validate(new AssignCategoryRequest("", Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EmptyTargetId_Fails()
    {
        ValidationResult result = _sut.Validate(new AssignCategoryRequest("X", Guid.Empty));
        result.IsValid.ShouldBeFalse();
    }
}
