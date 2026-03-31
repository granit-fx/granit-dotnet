using Granit.ReferenceData.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.ReferenceData.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="ReferenceDataUpdateRequest"/> body for reference data updates.
/// </summary>
internal sealed class ReferenceDataUpdateRequestValidator : GranitValidator<ReferenceDataUpdateRequest>
{
    public ReferenceDataUpdateRequestValidator()
    {
        Include(new ReferenceDataMutableFieldsValidator<ReferenceDataUpdateRequest>());
    }
}
