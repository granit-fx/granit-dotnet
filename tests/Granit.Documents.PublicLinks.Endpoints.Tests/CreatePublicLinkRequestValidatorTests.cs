using FluentValidation.Results;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.Endpoints.Dtos;
using Granit.Documents.PublicLinks.Endpoints.Validators;
using Granit.Documents.PublicLinks.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Endpoints.Tests;

public sealed class CreatePublicLinkRequestValidatorTests
{
    private static CreatePublicLinkRequestValidator NewValidator(int maxTtlDays = 90)
    {
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> monitor = Substitute.For<IOptionsMonitor<GranitDocumentsPublicLinksOptions>>();
        monitor.CurrentValue.Returns(new GranitDocumentsPublicLinksOptions
        {
            MaxTtl = TimeSpan.FromDays(maxTtlDays),
        });
        return new CreatePublicLinkRequestValidator(monitor);
    }

    [Fact]
    public void Valid_payload_should_pass()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 7, 10));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Null_MaxUses_should_pass()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.View, 1, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Zero_TtlDays_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 0, null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePublicLinkRequest.TtlDays));
    }

    [Fact]
    public void Negative_TtlDays_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, -5, null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TtlDays_at_max_should_pass()
    {
        CreatePublicLinkRequestValidator v = NewValidator(maxTtlDays: 90);
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 90, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TtlDays_above_max_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator(maxTtlDays: 90);
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 91, null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePublicLinkRequest.TtlDays));
    }

    [Fact]
    public void Zero_MaxUses_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 7, 0));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePublicLinkRequest.MaxUses));
    }

    [Fact]
    public void Negative_MaxUses_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest(PublicLinkScope.Download, 7, -3));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Out_of_range_scope_should_fail()
    {
        CreatePublicLinkRequestValidator v = NewValidator();
        ValidationResult result = v.Validate(new CreatePublicLinkRequest((PublicLinkScope)42, 7, null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePublicLinkRequest.Scope));
    }
}
