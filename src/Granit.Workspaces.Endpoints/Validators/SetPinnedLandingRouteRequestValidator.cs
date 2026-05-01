using FluentValidation;
using Granit.Workspaces.Endpoints.Dtos;

namespace Granit.Workspaces.Endpoints.Validators;

internal sealed class SetPinnedLandingRouteRequestValidator : AbstractValidator<SetPinnedLandingRouteRequest>
{
    public SetPinnedLandingRouteRequestValidator()
    {
        RuleFor(x => x.Route)
            .NotEmpty()
            .MaximumLength(2048)
            .When(x => x.Route is not null);
    }
}
