using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.MultiTenancy;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>Validates <see cref="RoleCreateRequest"/>.</summary>
internal sealed class RoleCreateRequestValidator : GranitValidator<RoleCreateRequest>
{
    public RoleCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.MultiTenancySide)
            .IsInEnum();

        // Side ↔ TenantId consistency (mirrors RoleMetadata.Create domain invariant).
        RuleFor(x => x)
            .Must(x => x.MultiTenancySide != MultiTenancySide.Tenant || x.TenantId is not null)
            .WithErrorCodeAndMessage("Granit:Identity:Role:TenantIdRequired");

        RuleFor(x => x)
            .Must(x => x.MultiTenancySide == MultiTenancySide.Tenant || x.TenantId is null)
            .WithErrorCodeAndMessage("Granit:Identity:Role:TenantIdForbidden");
    }
}
