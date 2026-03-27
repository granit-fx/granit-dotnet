using FluentValidation;
using Granit.BlobStorage.Endpoints.Dtos;

namespace Granit.BlobStorage.Endpoints.Validators;

internal sealed class BlobDownloadUrlRequestValidator : AbstractValidator<BlobDownloadUrlRequest>
{
    public BlobDownloadUrlRequestValidator()
    {
        RuleFor(x => x.ContainerName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.FileName)
            .MaximumLength(1024)
            .Matches(@"^[^\x00-\x1f""\\]*$")
            .When(x => x.FileName is not null);
    }
}
