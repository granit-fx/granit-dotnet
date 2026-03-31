using FluentValidation.Results;
using Granit.QueryEngine.Endpoints.Validators;
using Granit.QueryEngine.SavedViews;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests.Validators;

public sealed class UpdateSavedViewRequestValidatorTests
{
    private readonly UpdateSavedViewRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes_validation()
    {
        var request = new UpdateSavedViewRequest
        {
            Name = "Updated view name",
            IsShared = true,
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Empty_name_fails_validation()
    {
        var request = new UpdateSavedViewRequest
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
        var request = new UpdateSavedViewRequest
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
        var request = new UpdateSavedViewRequest
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
        var request = new UpdateSavedViewRequest
        {
            Name = new string('B', SavedViewRequestValidator<UpdateSavedViewRequest>.MaxNameLength),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Name_exceeding_max_length_fails_validation()
    {
        var request = new UpdateSavedViewRequest
        {
            Name = new string('B', SavedViewRequestValidator<UpdateSavedViewRequest>.MaxNameLength + 1),
        };

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }
}
