using FluentValidation;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Validators;

/// <summary>
/// Validator for <see cref="RenameDocumentRequest"/>.
/// </summary>
internal sealed class RenameDocumentRequestValidator : AbstractValidator<RenameDocumentRequest>
{
    public RenameDocumentRequestValidator()
    {
        // PATCH semantics: allow the name to be omitted entirely (null) but reject empty
        // strings — those would clear the document name, which the aggregate forbids.
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Document.MaxNameLength)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(Document.MaxDescriptionLength)
            .When(x => x.Description is not null);

        // No "must mutate something" rule: an empty payload is treated as a no-op by
        // the endpoint handler (returns the unchanged document). Adding a hardcoded
        // .WithMessage here would clash with the framework convention requiring a
        // localised error code for every custom rule (CLAUDE.md §Validation), and the
        // payload of error keys + 15 cultures is not worth the cost for a rare edge
        // case the handler already handles gracefully.
    }
}
