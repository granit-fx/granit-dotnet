using FluentValidation;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Validators;

internal sealed class MoveDocumentRequestValidator : AbstractValidator<MoveDocumentRequest>
{
    public MoveDocumentRequestValidator()
    {
        RuleFor(x => x.NewFolderId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.NewFolderId.HasValue);
    }
}
