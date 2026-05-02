using FluentValidation.Results;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Documents.Validators;

public sealed class AppendVersionRequestValidatorTests
{
    private readonly AppendVersionRequestValidator _validator = new();

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var request = new AppendVersionRequest(Guid.NewGuid(), "Patch typos in section 3");

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NullCommitMessage_PassesValidation()
    {
        var request = new AppendVersionRequest(Guid.NewGuid(), CommitMessage: null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyBlobId_Fails()
    {
        var request = new AppendVersionRequest(Guid.Empty, null);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CommitMessageTooLong_Fails()
    {
        var request = new AppendVersionRequest(
            Guid.NewGuid(),
            new string('m', DocumentVersion.MaxCommitMessageLength + 1));

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
