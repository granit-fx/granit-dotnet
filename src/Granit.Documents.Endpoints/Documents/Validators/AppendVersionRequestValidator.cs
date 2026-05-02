using FluentValidation;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Validators;

/// <summary>
/// Validator for <see cref="AppendVersionRequest"/>.
/// </summary>
internal sealed class AppendVersionRequestValidator : AbstractValidator<AppendVersionRequest>
{
    public AppendVersionRequestValidator()
    {
        RuleFor(x => x.BlobId)
            .NotEmpty();

        RuleFor(x => x.CommitMessage)
            .MaximumLength(DocumentVersion.MaxCommitMessageLength)
            .When(x => x.CommitMessage is not null);
    }
}
