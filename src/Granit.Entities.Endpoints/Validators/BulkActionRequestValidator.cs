using FluentValidation;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Options;
using Microsoft.Extensions.Options;

namespace Granit.Entities.Endpoints.Validators;

/// <summary>
/// Validates <see cref="BulkActionRequest"/> — non-empty <c>Ids</c> capped at
/// <see cref="EntitiesEndpointsOptions.BulkActionMaxIds"/> entries. Error
/// messages use framework error codes so the 18-culture
/// localization manager picks them up.
/// </summary>
internal sealed class BulkActionRequestValidator : AbstractValidator<BulkActionRequest>
{
    public BulkActionRequestValidator(IOptions<EntitiesEndpointsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        int maxIds = options.Value.BulkActionMaxIds;

        RuleFor(x => x.Ids)
            .NotEmpty();

        RuleFor(x => x.Ids.Count)
            .LessThanOrEqualTo(maxIds)
            .When(x => x.Ids is not null);
    }
}
