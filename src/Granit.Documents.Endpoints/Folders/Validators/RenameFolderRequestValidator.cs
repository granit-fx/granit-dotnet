using FluentValidation;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;

namespace Granit.Documents.Endpoints.Folders.Validators;

/// <summary>
/// Validator for <see cref="RenameFolderRequest"/>.
/// </summary>
internal sealed class RenameFolderRequestValidator : AbstractValidator<RenameFolderRequest>
{
    public RenameFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Folder.MaxNameLength)
            .Matches("^[^/]+$");
    }
}
