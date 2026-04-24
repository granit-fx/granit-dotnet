using FluentValidation;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Catalog.Endpoints.Validators;

/// <summary>
/// Validates <see cref="ProductUpdateRequest"/>.
/// </summary>
internal sealed class ProductUpdateRequestValidator : GranitValidator<ProductUpdateRequest>
{
    public ProductUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(ProductCreateRequestValidator.MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(ProductCreateRequestValidator.MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Unit)
            .NotEmpty()
            .MaximumLength(ProductCreateRequestValidator.MaxUnitLength);
    }
}
