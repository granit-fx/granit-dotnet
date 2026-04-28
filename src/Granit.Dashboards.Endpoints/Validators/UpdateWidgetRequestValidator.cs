using FluentValidation;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Dashboards.Endpoints.Validators;

/// <summary>
/// Validates <see cref="UpdateWidgetRequest"/>. Mirrors the constraints in
/// <see cref="AddWidgetRequestValidator"/> — the same domain guards apply on
/// create and update; HTTP-layer validation kept symmetric so callers see the
/// same error codes.
/// </summary>
internal sealed class UpdateWidgetRequestValidator : GranitValidator<UpdateWidgetRequest>
{
    public UpdateWidgetRequestValidator()
    {
        RuleFor(x => x.TitleLocalizationKey)
            .NotEmpty()
            .MaximumLength(AddWidgetRequestValidator.MaxLocalizationKeyLength);

        RuleFor(x => x.ConfigJson)
            .NotEmpty()
            .MaximumLength(AddWidgetRequestValidator.MaxConfigJsonLength);

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Width)
            .GreaterThan(0);

        RuleFor(x => x.Height)
            .GreaterThan(0);
    }
}
