using Granit.Geocoding.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Geocoding.Endpoints.Validators;

/// <summary>Validates <see cref="GeocodingReverseRequest"/> — coordinate within valid bounds.</summary>
internal sealed class GeocodingReverseRequestValidator : GranitValidator<GeocodingReverseRequest>
{
    public GeocodingReverseRequestValidator()
    {
        RuleFor(x => x.Lat).GeoLatitude();
        RuleFor(x => x.Lon).GeoLongitude();
    }
}
