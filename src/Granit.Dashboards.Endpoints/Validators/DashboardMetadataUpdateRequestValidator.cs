using FluentValidation;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Dashboards.Endpoints.Validators;

/// <summary>
/// Validates <see cref="DashboardMetadataUpdateRequest"/>. Built-in rules
/// (NotEmpty, MaximumLength, GreaterThan) are auto-mapped to localized error
/// codes by <c>GranitErrorCodeLanguageManager</c> — no hardcoded messages.
/// </summary>
internal sealed class DashboardMetadataUpdateRequestValidator : GranitValidator<DashboardMetadataUpdateRequest>
{
    internal const int MaxNameLength = 200;

    public DashboardMetadataUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.LayoutColumns)
            .GreaterThan(0);

        RuleFor(x => x.LayoutRowHeight)
            .GreaterThan(0);
    }
}
