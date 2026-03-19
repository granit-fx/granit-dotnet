using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobConfirmUploadRequestValidator : AbstractValidator<BlobConfirmUploadRequest>
{
    public BlobConfirmUploadRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128);
    }
}
