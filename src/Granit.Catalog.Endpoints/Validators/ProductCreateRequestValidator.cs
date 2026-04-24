using FluentValidation;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Catalog.Endpoints.Validators;

/// <summary>
/// Validates <see cref="ProductCreateRequest"/>.
/// </summary>
internal sealed class ProductCreateRequestValidator : GranitValidator<ProductCreateRequest>
{
    internal const int MaxSkuLength = 64;
    internal const int MaxNameLength = 200;
    internal const int MaxDescriptionLength = 2000;
    internal const int MaxUnitLength = 64;

    public ProductCreateRequestValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(MaxSkuLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Type)
            .NotEmpty();

        RuleFor(x => x.Unit)
            .NotEmpty()
            .MaximumLength(MaxUnitLength);
    }
}
