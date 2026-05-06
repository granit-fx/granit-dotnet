using FluentValidation.Results;
using Granit.Taxonomy.Endpoints.Tags.Dtos;
using Granit.Taxonomy.Endpoints.Tags.Validators;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Tags.Validators;

public sealed class AssignTagRequestValidatorTests
{
    private readonly AssignTagRequestValidator _sut = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        ValidationResult result = _sut.Validate(new AssignTagRequest(
            "Granit.Documents.Domain.Document", Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTargetType_Fails(string targetType)
    {
        ValidationResult result = _sut.Validate(new AssignTagRequest(targetType, Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TargetTypeTooLong_Fails()
    {
        string tooLong = new('x', 257);
        ValidationResult result = _sut.Validate(new AssignTagRequest(tooLong, Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EmptyTargetId_Fails()
    {
        ValidationResult result = _sut.Validate(new AssignTagRequest("X", Guid.Empty));
        result.IsValid.ShouldBeFalse();
    }
}
