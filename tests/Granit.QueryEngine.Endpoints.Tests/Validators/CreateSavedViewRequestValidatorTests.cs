using FluentValidation.Results;
using Granit.QueryEngine.Endpoints.Validators;
using Granit.QueryEngine.SavedViews;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests.Validators;

public sealed class CreateSavedViewRequestValidatorTests
{
    private readonly CreateSavedViewRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = "Active patients",
            IsShared = false,
            IsDefault = true,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Empty_name_fails_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = string.Empty,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Null_name_fails_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = null!,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Whitespace_name_fails_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = "   ",
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Name_at_max_length_passes_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = new string('A', SavedViewRequestValidator<CreateSavedViewRequest>.MaxNameLength),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Name_exceeding_max_length_fails_validation()
    {
        var request = new CreateSavedViewRequest
        {
            Name = new string('A', SavedViewRequestValidator<CreateSavedViewRequest>.MaxNameLength + 1),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }
}
