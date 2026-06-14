using FluentValidation;
using Granit.AI.Prompts.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.AI.Prompts.Endpoints.Validators;

/// <summary>Validates <see cref="CreatePromptRequest"/>.</summary>
internal sealed class CreatePromptRequestValidator : GranitValidator<CreatePromptRequest>
{
    /// <summary>Maximum prompt name length.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Maximum short-description length.</summary>
    public const int MaxShortDescriptionLength = 500;

    /// <summary>Maximum instruction-text length.</summary>
    public const int MaxContentLength = 20000;

    /// <summary>Maximum icon-identifier length.</summary>
    public const int MaxIconLength = 100;

    /// <summary>Maximum number of categories a prompt may be filed under.</summary>
    public const int MaxCategories = 10;

    public CreatePromptRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(MaxContentLength);
        RuleFor(x => x.ShortDescription).MaximumLength(MaxShortDescriptionLength);
        RuleFor(x => x.Icon).MaximumLength(MaxIconLength);
        RuleFor(x => x.IconColor).ColorHex().When(x => !string.IsNullOrWhiteSpace(x.IconColor));

        RuleFor(x => x.CategoryIds)
            .Must(c => c is null || c.Count <= MaxCategories)
            .WithErrorCodeAndMessage("AIPrompts:Validation:TooManyCategories");
    }
}
