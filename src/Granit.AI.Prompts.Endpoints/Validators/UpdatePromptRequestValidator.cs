using FluentValidation;
using Granit.AI.Prompts.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.AI.Prompts.Endpoints.Validators;

/// <summary>Validates <see cref="UpdatePromptRequest"/>.</summary>
internal sealed class UpdatePromptRequestValidator : GranitValidator<UpdatePromptRequest>
{
    public UpdatePromptRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CreatePromptRequestValidator.MaxNameLength);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(CreatePromptRequestValidator.MaxContentLength);
        RuleFor(x => x.ShortDescription).MaximumLength(CreatePromptRequestValidator.MaxShortDescriptionLength);
        RuleFor(x => x.Icon).MaximumLength(CreatePromptRequestValidator.MaxIconLength);
        RuleFor(x => x.IconColor).ColorHex().When(x => !string.IsNullOrWhiteSpace(x.IconColor));

        RuleFor(x => x.CategoryIds)
            .Must(c => c is null || c.Count <= CreatePromptRequestValidator.MaxCategories)
            .WithErrorCodeAndMessage("AIPrompts:Validation:TooManyCategories");
    }
}
