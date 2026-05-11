using FluentValidation;
using Granit.Documents.PublicLinks.Endpoints.Dtos;

namespace Granit.Documents.PublicLinks.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="RevokePublicLinkRequest"/>. The reason is optional;
/// when present it must stay within <see cref="RevokePublicLinkRequest.MaxReasonLength"/>.
/// </summary>
internal sealed class RevokePublicLinkRequestValidator : AbstractValidator<RevokePublicLinkRequest>
{
    public RevokePublicLinkRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(RevokePublicLinkRequest.MaxReasonLength)
            .When(x => x.Reason is not null);
    }
}
