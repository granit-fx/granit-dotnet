using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyExternalMappingRequestValidator : GranitValidator<PartyExternalMappingRequest>
{
    public PartyExternalMappingRequestValidator()
    {
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(256);
    }
}
