using FluentValidation;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Dashboards.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AddWidgetRequest"/>. Built-in rules auto-localize via
/// <c>GranitErrorCodeLanguageManager</c>; the domain guards in
/// <c>WidgetInstance.Create</c> stay as defense-in-depth.
/// </summary>
internal sealed class AddWidgetRequestValidator : GranitValidator<AddWidgetRequest>
{
    internal const int MaxWidgetTypeLength = 100;
    internal const int MaxLocalizationKeyLength = 200;
    internal const int MaxConfigJsonLength = 16_000;
    internal const int MaxPermissionLength = 200;

    public AddWidgetRequestValidator()
    {
        RuleFor(x => x.WidgetType)
            .NotEmpty()
            .MaximumLength(MaxWidgetTypeLength);

        RuleFor(x => x.TitleLocalizationKey)
            .NotEmpty()
            .MaximumLength(MaxLocalizationKeyLength);

        RuleFor(x => x.ConfigJson)
            .NotEmpty()
            .MaximumLength(MaxConfigJsonLength);

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Width)
            .GreaterThan(0);

        RuleFor(x => x.Height)
            .GreaterThan(0);

        RuleFor(x => x.MetricName)
            .MaximumLength(MaxLocalizationKeyLength)
            .When(x => x.MetricName is not null);

        RuleFor(x => x.QueryName)
            .MaximumLength(MaxLocalizationKeyLength)
            .When(x => x.QueryName is not null);

        RuleFor(x => x.RequiredPermission)
            .MaximumLength(MaxPermissionLength)
            .When(x => x.RequiredPermission is not null);
    }
}
