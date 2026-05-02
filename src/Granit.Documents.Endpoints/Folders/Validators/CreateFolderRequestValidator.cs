using FluentValidation;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;

namespace Granit.Documents.Endpoints.Folders.Validators;

/// <summary>
/// Validator for <see cref="CreateFolderRequest"/>.
/// </summary>
internal sealed class CreateFolderRequestValidator : AbstractValidator<CreateFolderRequest>
{
    public CreateFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Folder.MaxNameLength)
            // Path separator '/' is reserved for the materialised folder path.
            .Matches("^[^/]+$");
    }
}
