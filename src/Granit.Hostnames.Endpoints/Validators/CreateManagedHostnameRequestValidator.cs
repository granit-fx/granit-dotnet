using FluentValidation;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Hostnames.Endpoints.Validators;

/// <summary>
/// Validates <see cref="CreateManagedHostnameRequest"/> (structural constraints only).
/// Domain validation — FQDN format, global uniqueness — is enforced by
/// <see cref="Granit.Hostnames.Domain.Hostname.Create"/> and the write-side persistence layer.
/// </summary>
internal sealed class CreateManagedHostnameRequestValidator
    : GranitValidator<CreateManagedHostnameRequest>
{
    internal const int MaxHostLength = 253;
    internal const int MaxOwnerTypeLength = 100;

    public CreateManagedHostnameRequestValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(MaxHostLength);

        RuleFor(x => x.OwnerType)
            .NotEmpty()
            .MaximumLength(MaxOwnerTypeLength);

        RuleFor(x => x.OwnerId)
            .NotEmpty();
    }
}
