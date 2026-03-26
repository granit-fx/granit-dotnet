using FluentValidation;
using Granit.Auditing.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Auditing.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AuditingQueryParameters"/> query parameters.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class AuditingQueryParametersValidator : GranitValidator<AuditingQueryParameters>
{
    internal const int MaxStringFilterLength = 500;

    public AuditingQueryParametersValidator()
    {
        RuleFor(x => x.Page).ValidPage();

        RuleFor(x => x.PageSize).ValidPageSize();

        RuleFor(x => x.UserId).MaximumLength(MaxStringFilterLength);

        RuleFor(x => x.EntityType).MaximumLength(MaxStringFilterLength);

        RuleFor(x => x.EntityId).MaximumLength(MaxStringFilterLength);

        RuleFor(x => x.From)
            .LessThan(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
