using FluentValidation.Results;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Documents.Validators;

public sealed class FinalizeUploadRequestValidatorTests
{
    private readonly FinalizeUploadRequestValidator _validator = new();

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var request = new FinalizeUploadRequest(
            BlobId: Guid.NewGuid(),
            FolderId: null,
            Name: "Q1-2026.pdf",
            Description: "Quarter 1 close",
            CommitMessage: "Initial upload");

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyBlobId_Fails()
    {
        var request = new FinalizeUploadRequest(Guid.Empty, null, "F.pdf", null, null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyName_Fails(string name)
    {
        var request = new FinalizeUploadRequest(Guid.NewGuid(), null, name, null, null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task NameTooLong_Fails()
    {
        var request = new FinalizeUploadRequest(
            Guid.NewGuid(), null, new string('a', Document.MaxNameLength + 1), null, null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task DescriptionTooLong_Fails()
    {
        var request = new FinalizeUploadRequest(
            Guid.NewGuid(),
            null,
            "F.pdf",
            new string('d', Document.MaxDescriptionLength + 1),
            null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CommitMessageTooLong_Fails()
    {
        var request = new FinalizeUploadRequest(
            Guid.NewGuid(),
            null,
            "F.pdf",
            null,
            new string('m', DocumentVersion.MaxCommitMessageLength + 1));

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
