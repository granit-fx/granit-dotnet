using FluentValidation.Results;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Documents.Validators;

public sealed class RenameDocumentRequestValidatorTests
{
    private readonly RenameDocumentRequestValidator _validator = new();

    [Fact]
    public async Task RenameOnly_PassesValidation()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest("New.pdf", null, ClearDescription: false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task DescriptionOnly_PassesValidation()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(null, "An updated description", false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearDescription_OnlyFlag_PassesValidation()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(null, null, ClearDescription: true),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyPayload_PassesValidation_HandlerTreatsAsNoOp()
    {
        // The handler returns the unchanged document on a fully-empty payload — no
        // hardcoded validation error here so we stay aligned with the framework
        // convention requiring localised error codes for every custom .Must rule.
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(null, null, false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(string.Empty, null, false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task NameTooLong_Fails()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(new string('a', Document.MaxNameLength + 1), null, false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task DescriptionTooLong_Fails()
    {
        ValidationResult result = await _validator.ValidateAsync(
            new RenameDocumentRequest(null, new string('d', Document.MaxDescriptionLength + 1), false),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
