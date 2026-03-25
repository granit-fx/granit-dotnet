using FluentValidation;
using Granit.Auditing.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Auditing.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AuditingQueryRequest"/> query parameters.
/// Auto-discovered by <c>GranitValidationModule</c>.
/// </summary>
internal sealed class AuditingQueryRequestValidator : GranitValidator<AuditingQueryRequest>
{
    internal const int MinPage = 1;
    internal const int MinPageSize = 1;
    internal const int MaxPageSize = 100;
    internal const int MaxStringFilterLength = 500;

    public AuditingQueryRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(MinPage)
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(MinPageSize)
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(MaxPageSize)
            .When(x => x.PageSize.HasValue);

        RuleFor(x => x.UserId)
            .MaximumLength(MaxStringFilterLength)
            .When(x => x.UserId is not null);

        RuleFor(x => x.EntityType)
            .MaximumLength(MaxStringFilterLength)
            .When(x => x.EntityType is not null);

        RuleFor(x => x.EntityId)
            .MaximumLength(MaxStringFilterLength)
            .When(x => x.EntityId is not null);

        RuleFor(x => x.From)
            .LessThan(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
