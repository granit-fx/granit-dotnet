using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobUploadInitiateRequestValidator : AbstractValidator<BlobUploadInitiateRequest>
{
    public BlobUploadInitiateRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithMessage("Container name must be lowercase alphanumeric with optional hyphens.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(256)
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9!#$&\\-^_.+]*\\/[a-zA-Z0-9][a-zA-Z0-9!#$&\\-^_.+]*$")
            .WithMessage("Content type must be a valid MIME type.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0);
    }
}
