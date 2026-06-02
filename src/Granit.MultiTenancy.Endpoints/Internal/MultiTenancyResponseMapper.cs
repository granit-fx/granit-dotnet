using Granit.MultiTenancy.Endpoints.Dtos;
using Granit.MultiTenancy.Stores;

namespace Granit.MultiTenancy.Endpoints.Internal;

/// <summary>
/// Maps <see cref="TenantData"/> store results to response DTOs.
/// </summary>
internal static class MultiTenancyResponseMapper
{
    /// <summary>Maps a <see cref="TenantData"/> to a <see cref="TenantResponse"/>.</summary>
    public static TenantResponse ToResponse(TenantData data) =>
        new(data.Id, data.Name, data.Identifier, data.ContactEmail, data.Activated, data.Jurisdiction, data.CreatedAt, data.ConcurrencyStamp);
}
