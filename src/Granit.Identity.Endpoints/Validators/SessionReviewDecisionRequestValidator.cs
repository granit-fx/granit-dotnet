using FluentValidation;
using Granit.Identity.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="SessionReviewDecisionRequest"/> body: the review token must be present (the token
/// itself is validated cryptographically by the endpoint).
/// </summary>
internal sealed class SessionReviewDecisionRequestValidator : GranitValidator<SessionReviewDecisionRequest>
{
    public SessionReviewDecisionRequestValidator() => RuleFor(x => x.Token).NotEmpty();
}
