using FluentValidation;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Endpoints.Dtos;
using Granit.Entities.Customization.Endpoints.Options;
using Granit.Validation.Extensions;
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
            .WithErrorCodeAndMessage("Granit:Validation:TooManyDeltas");

        RuleForEach(x => x.Deltas).ChildRules(child =>
        {
            child.RuleFor(d => d.FieldName).NotEmpty();

            child.RuleFor(d => d)
                .Must(d => d is not ReorderDelta r || r.IsAnchorWellFormed)
                .WithErrorCodeAndMessage("Granit:Validation:ReorderDeltaIllFormed");

            child.RuleFor(d => d)
                .Must(d => d is not RegroupDelta r || !string.IsNullOrWhiteSpace(r.GroupKey))
                .WithErrorCodeAndMessage("Granit:Validation:RegroupGroupKeyRequired");
        });
    }
}
