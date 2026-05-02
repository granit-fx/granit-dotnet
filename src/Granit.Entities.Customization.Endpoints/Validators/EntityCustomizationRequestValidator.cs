using FluentValidation;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Endpoints.Dtos;
using Granit.Entities.Customization.Endpoints.Options;
using Microsoft.Extensions.Options;

namespace Granit.Entities.Customization.Endpoints.Validators;

/// <summary>
/// Shape-level validation for <see cref="EntityCustomizationRequest"/>.
/// Semantic validation against the live <c>EntityDefinitionDescriptor</c>
/// happens in the endpoint handler via the descriptor validator — keeps
/// FluentValidation purely declarative + descriptor-free.
/// </summary>
internal sealed class EntityCustomizationRequestValidator : AbstractValidator<EntityCustomizationRequest>
{
    public EntityCustomizationRequestValidator(IOptions<EntitiesCustomizationEndpointsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        int maxDeltas = options.Value.MaxDeltasPerRequest;

        RuleFor(x => x.Deltas).NotNull();

        RuleFor(x => x.Deltas)
            .Must(d => d.Count <= maxDeltas)
            .WithMessage($"At most {maxDeltas} deltas may be sent in a single request.");

        RuleForEach(x => x.Deltas).ChildRules(child =>
        {
            child.RuleFor(d => d.FieldName).NotEmpty();
            child.RuleFor(d => d).Custom((delta, ctx) =>
            {
                if (delta is ReorderDelta reorder && !reorder.IsAnchorWellFormed)
                {
                    ctx.AddFailure(
                        nameof(ReorderDelta),
                        $"ReorderDelta for '{reorder.FieldName}' must set exactly one of BeforeFieldName / AfterFieldName.");
                }
                if (delta is RegroupDelta regroup && string.IsNullOrWhiteSpace(regroup.GroupKey))
                {
                    ctx.AddFailure(nameof(RegroupDelta.GroupKey), "GroupKey is required.");
                }
            });
        });
    }
}
