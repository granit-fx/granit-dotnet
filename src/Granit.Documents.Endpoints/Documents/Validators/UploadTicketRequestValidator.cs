using FluentValidation;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Validators;

/// <summary>
/// Validator for <see cref="UploadTicketRequest"/>.
/// </summary>
internal sealed class UploadTicketRequestValidator : AbstractValidator<UploadTicketRequest>
{
    /// <summary>Maximum upload ceiling enforced at the API edge — 10 GiB by default.</summary>
    /// <remarks>
    /// BlobStorage providers may enforce a tighter per-tenant ceiling; this is the absolute
    /// maximum the API will issue tickets for.
    /// </remarks>
    public const long MaxAllowedBytesCeiling = 10L * 1024L * 1024L * 1024L;

    public UploadTicketRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(127);

        RuleFor(x => x.MaxAllowedBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxAllowedBytesCeiling);
    }
}
