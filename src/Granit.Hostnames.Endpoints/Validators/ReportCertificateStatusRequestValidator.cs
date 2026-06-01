using FluentValidation;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Hostnames.Endpoints.Validators;

/// <summary>
/// Validates <see cref="ReportCertificateStatusRequest"/>.
/// Enforces that <see cref="ReportCertificateStatusRequest.ExpiresAt"/> is provided
/// when <see cref="CertificateStatus.Secured"/> is reported.
/// </summary>
internal sealed class ReportCertificateStatusRequestValidator
    : GranitValidator<ReportCertificateStatusRequest>
{
    public ReportCertificateStatusRequestValidator()
    {
        RuleFor(x => x.ExpiresAt)
            .NotNull()
            .When(x => x.Status == CertificateStatus.Secured);
    }
}
