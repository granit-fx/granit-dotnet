using FluentValidation;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Validators;

/// <summary>
/// Validator for <see cref="FinalizeUploadRequest"/>.
/// </summary>
internal sealed class FinalizeUploadRequestValidator : AbstractValidator<FinalizeUploadRequest>
{
    public FinalizeUploadRequestValidator()
    {
        RuleFor(x => x.BlobId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Document.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(Document.MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.CommitMessage)
            .MaximumLength(DocumentVersion.MaxCommitMessageLength)
            .When(x => x.CommitMessage is not null);
    }
}
