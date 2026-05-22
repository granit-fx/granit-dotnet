using FluentValidation;
using Granit.Presence.Domain;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Options;
using Granit.Timing;
using Granit.Validation;
using Granit.Validation.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Presence.Endpoints.Validators;

internal sealed class SetPresenceRequestValidator : GranitValidator<SetPresenceRequest>
{
    public SetPresenceRequestValidator(IClock clock, IOptionsMonitor<PresenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(x => x.ManualStatus)
            .IsInEnum();

        RuleFor(x => x.UntilUtc)
            .Must(until => until is null || until.Value > clock.Now.AddSeconds(1))
                .WithErrorCodeAndMessage("Granit:Validation:PresenceOverrideUntilInPast")
            .Must(until => until is null || until.Value <= clock.Now + options.CurrentValue.MaxOverrideDuration)
                .WithErrorCodeAndMessage("Granit:Validation:PresenceOverrideUntilTooFar");

        RuleFor(x => x)
            .Must(x => x.ManualStatus != ManualPresenceStatus.Available || x.UntilUtc is null)
                .WithErrorCodeAndMessage("Granit:Validation:PresenceUntilWithoutOverride");
    }
}
