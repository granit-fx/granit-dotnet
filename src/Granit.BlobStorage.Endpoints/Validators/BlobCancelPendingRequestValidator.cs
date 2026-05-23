using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobCancelPendingRequestValidator : AbstractValidator<BlobCancelPendingRequest>
{
    public BlobCancelPendingRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(512);
    }
}
