using FluentValidation;
using Granit.Invoicing.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Invoicing.Endpoints.Validators;

/// <summary>
/// Validates <see cref="InvoiceCreateRequest"/>.
/// </summary>
internal sealed class InvoiceCreateRequestValidator : GranitValidator<InvoiceCreateRequest>
{
    internal const int MaxCurrencyLength = 3;
    internal const int MaxCreditNoteReasonLength = 500;

    public InvoiceCreateRequestValidator()
    {
        // ContactId is required — built-in NotEmpty rejects Guid.Empty and the
        // GranitErrorCodeLanguageManager auto-localises the error.
        RuleFor(x => x.ContactId)
            .NotEmpty();

        RuleFor(x => x.DocumentType)
            .IsInEnum();

        RuleFor(x => x.Currency)
            .NotEmpty()
            .MaximumLength(MaxCurrencyLength);

        RuleFor(x => x.CollectionMethod)
            .IsInEnum();

        RuleFor(x => x.BillingReason)
            .IsInEnum();

        RuleFor(x => x.CreditNoteReason)
            .MaximumLength(MaxCreditNoteReasonLength)
            .When(x => x.CreditNoteReason is not null);

        RuleFor(x => x.PeriodEnd)
            .Must((req, periodEnd) => periodEnd > req.PeriodStart)
            .When(x => x.PeriodStart.HasValue && x.PeriodEnd.HasValue)
            .WithErrorCodeAndMessage("Granit:Validation:PeriodEndMustBeAfterStart");
    }
}
