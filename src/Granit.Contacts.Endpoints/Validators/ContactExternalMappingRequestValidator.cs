using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactExternalMappingRequestValidator : GranitValidator<ContactExternalMappingRequest>
{
    public ContactExternalMappingRequestValidator()
    {
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(256);
    }
}
