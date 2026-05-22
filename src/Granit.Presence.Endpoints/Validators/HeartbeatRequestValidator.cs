using FluentValidation;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Options;
using Granit.Validation;
using Granit.Validation.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Presence.Endpoints.Validators;

internal sealed class HeartbeatRequestValidator : GranitValidator<HeartbeatRequest>
{
    public HeartbeatRequestValidator(IOptionsMonitor<PresenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(x => x.IdleSeconds)
            .GreaterThanOrEqualTo(0)
                .WithErrorCodeAndMessage("Granit:Validation:PresenceIdleSecondsNegative")
            .Must(seconds => seconds <= (int)(options.CurrentValue.OfflineThreshold.TotalSeconds * 2))
                .WithErrorCodeAndMessage("Granit:Validation:PresenceIdleSecondsTooLarge");
    }
}
