using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobUploadInitiateRequestValidator : AbstractValidator<BlobUploadInitiateRequest>
{
    public BlobUploadInitiateRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithErrorCodeAndMessage("Validation:InvalidContainerName");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(256)
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9!#$&\\-^_.+]*\\/[a-zA-Z0-9][a-zA-Z0-9!#$&\\-^_.+]*$")
            .WithErrorCodeAndMessage("Validation:InvalidMimeType");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0);
    }
}
