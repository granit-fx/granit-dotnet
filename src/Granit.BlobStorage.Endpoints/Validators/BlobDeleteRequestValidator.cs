using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobDeleteRequestValidator : AbstractValidator<BlobDeleteRequest>
{
    public BlobDeleteRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.DeletionReason)
            .MaximumLength(500)
            .When(x => x.DeletionReason is not null);
    }
}
