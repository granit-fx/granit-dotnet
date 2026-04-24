using FluentValidation;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Catalog.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AddProductExternalMappingRequest"/>.
/// </summary>
internal sealed class AddProductExternalMappingRequestValidator : GranitValidator<AddProductExternalMappingRequest>
{
    internal const int MaxProviderNameLength = 64;
    internal const int MaxExternalIdLength = 256;

    public AddProductExternalMappingRequestValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .MaximumLength(MaxProviderNameLength);

        RuleFor(x => x.ExternalId)
            .NotEmpty()
            .MaximumLength(MaxExternalIdLength);
    }
}
