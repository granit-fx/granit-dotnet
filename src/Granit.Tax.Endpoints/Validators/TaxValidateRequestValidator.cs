using FluentValidation;
using Granit.Tax.Endpoints.Dtos;

namespace Granit.Tax.Endpoints.Validators;

internal sealed class TaxValidateRequestValidator : AbstractValidator<TaxValidateRequest>
{
    public TaxValidateRequestValidator()
    {
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
    }
}
