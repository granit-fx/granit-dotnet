using FluentValidation.Results;
using Granit.Activities.Endpoints.Dtos;
using Granit.Activities.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Activities.Endpoints.Tests;

public sealed class CreateActivityRequestValidatorTests
{
    private static readonly CreateActivityRequestValidator Sut = new();

    private static CreateActivityRequest ValidRequest() => new(
        EntityType: "Granit.Parties.Party",
        EntityId: Guid.NewGuid(),
        Type: "ToDo",
        AssignedToUserId: Guid.NewGuid(),
        DueAt: DateTimeOffset.UtcNow.AddDays(1));

    [Fact]
    public void Valid_request_passes()
    {
        ValidationResult result = Sut.Validate(ValidRequest());
        result.IsValid.ShouldBeTrue($"errors: {string.Join(", ", result.Errors)}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EntityType_empty_or_whitespace_fails(string entityType)
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { EntityType = entityType });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EntityType_over_256_chars_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { EntityType = new string('x', 257) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void EntityId_empty_guid_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { EntityId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Type_empty_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { Type = "" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Type_over_64_chars_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { Type = new string('x', 65) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AssignedToUserId_empty_guid_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { AssignedToUserId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Description_over_2000_chars_fails()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { Description = new string('x', 2_001) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Description_null_passes()
    {
        ValidationResult result = Sut.Validate(ValidRequest() with { Description = null });
        result.IsValid.ShouldBeTrue();
    }
}
