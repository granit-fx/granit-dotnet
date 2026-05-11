using FluentValidation;
using Granit.Documents.PublicLinks.Endpoints.Dtos;
using Granit.Documents.PublicLinks.Options;
using Microsoft.Extensions.Options;

namespace Granit.Documents.PublicLinks.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="CreatePublicLinkRequest"/>. Rules:
/// <list type="bullet">
///   <item><c>TtlDays</c> must be ≥ 1 and not exceed <c>GranitDocumentsPublicLinksOptions.MaxTtl</c>
///     converted to whole days (rounded up).</item>
///   <item><c>MaxUses</c>, when supplied, must be strictly positive.</item>
/// </list>
/// Messages use FluentValidation's built-in error codes (auto-localised by
/// <c>Granit.Validation</c>) — no custom <c>WithMessage</c> calls required.
/// </summary>
internal sealed class CreatePublicLinkRequestValidator : AbstractValidator<CreatePublicLinkRequest>
{
    public CreatePublicLinkRequestValidator(
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        int maxTtlDays = Math.Max(1, (int)Math.Ceiling(optionsMonitor.CurrentValue.MaxTtl.TotalDays));

        RuleFor(x => x.TtlDays)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(maxTtlDays);

        RuleFor(x => x.MaxUses)
            .GreaterThan(0)
            .When(x => x.MaxUses.HasValue);

        RuleFor(x => x.Scope)
            .IsInEnum();
    }
}
