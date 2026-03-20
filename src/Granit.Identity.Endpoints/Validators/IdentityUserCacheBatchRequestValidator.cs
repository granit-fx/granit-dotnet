using FluentValidation;
using Granit.Identity.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Identity.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="IdentityUserCacheBatchRequest"/> body for batch user resolution.
/// </summary>
internal sealed class IdentityUserCacheBatchRequestValidator : GranitValidator<IdentityUserCacheBatchRequest>
{
    /// <summary>Maximum number of user IDs in a single batch request.</summary>
    internal const int MaxBatchSize = 100;

    /// <summary>Maximum length for a single external user ID (must match <c>UserCacheModelBuilderExtensions</c>).</summary>
    internal const int MaxUserIdLength = 256;

    public IdentityUserCacheBatchRequestValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty()
            .Must(ids => ids.Count <= MaxBatchSize)
            .WithErrorCodeAndMessage("Granit:Validation:MaxBatchSize");

        RuleForEach(x => x.UserIds)
            .NotEmpty()
            .MaximumLength(MaxUserIdLength);
    }
}
