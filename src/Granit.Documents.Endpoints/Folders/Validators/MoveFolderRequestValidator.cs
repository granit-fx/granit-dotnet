using FluentValidation;
using Granit.Documents.Endpoints.Folders.Dtos;

namespace Granit.Documents.Endpoints.Folders.Validators;

internal sealed class MoveFolderRequestValidator : AbstractValidator<MoveFolderRequest>
{
    public MoveFolderRequestValidator()
    {
        RuleFor(x => x.NewParentFolderId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.NewParentFolderId.HasValue);
    }
}
