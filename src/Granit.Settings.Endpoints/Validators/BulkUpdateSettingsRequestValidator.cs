using FluentValidation;
using Granit.Settings.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Settings.Endpoints.Validators;

/// <summary>
/// Validates the envelope of a <see cref="BulkUpdateSettingsRequest"/>. Per-entry semantic
/// validation (setting existence, provider allow-list, ValueKind/AllowedValues) happens at
/// handler level and is reported through <see cref="BulkSettingResult"/>.
/// </summary>
internal sealed class BulkUpdateSettingsRequestValidator : GranitValidator<BulkUpdateSettingsRequest>
{
    internal const int MaxEntries = 200;
    internal const int MaxKeyLength = 256;

    public BulkUpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Settings)
            .NotEmpty()
            .Must(s => s is null || s.Count <= MaxEntries)
            .WithErrorCodeAndMessage("Validation:BulkSettingsTooLarge");

        RuleForEach(x => x.Settings).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Key)
                .NotEmpty()
                .MaximumLength(MaxKeyLength);

            entry.RuleFor(e => e.Value)
                .MaximumLength(UpdateSettingValueRequestValidator.MaxValueLength)
                .When(e => e.Value is not null);
        });
    }
}
