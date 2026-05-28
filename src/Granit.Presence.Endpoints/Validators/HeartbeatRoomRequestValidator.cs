using System.Text;
using FluentValidation;
using Granit.Presence.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Presence.Endpoints.Validators;

internal sealed class HeartbeatRoomRequestValidator : GranitValidator<HeartbeatRoomRequest>
{
    /// <summary>Maximum metadata UTF-8 byte size accepted on a heartbeat.</summary>
    public const int MaxMetadataBytes = 512;

    public HeartbeatRoomRequestValidator()
    {
        RuleFor(x => x.Metadata)
            .Must(m => m is null || Encoding.UTF8.GetByteCount(m) <= MaxMetadataBytes)
            .WithErrorCodeAndMessage("Granit:Validation:PresenceRoomMetadataTooLarge");
    }
}
