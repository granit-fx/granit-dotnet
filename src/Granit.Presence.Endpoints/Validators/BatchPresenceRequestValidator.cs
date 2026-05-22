using FluentValidation;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Options;
using Granit.Validation;
using Granit.Validation.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Presence.Endpoints.Validators;

internal sealed class BatchPresenceRequestValidator : GranitValidator<BatchPresenceRequest>
{
    public BatchPresenceRequestValidator(IOptionsMonitor<PresenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(x => x.UserIds)
            .NotNull()
            .NotEmpty()
            .Must(ids => ids.Count <= options.CurrentValue.MaxBatchSize)
                .WithErrorCodeAndMessage("Granit:Validation:PresenceBatchTooLarge")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithErrorCodeAndMessage("Granit:Validation:PresenceBatchDuplicates");
    }
}
