using System.Text.Json;
using Granit.Identity.Local.Domain;
using Granit.Privacy.DataExport;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.Wolverine.DataExport;

/// <summary>
/// Privacy data provider for Granit.Identity.Local. Emits the authenticated user's profile
/// (<see cref="GranitUser"/>) and the roles they belong to as a JSON fragment during the
/// scatter-gather personal-data export saga (GDPR Art. 15/20).
/// </summary>
/// <remarks>
/// Returns <see cref="ReadOnlyMemory{T}.Empty"/> when the user has been hard-deleted or was
/// never provisioned locally — the <c>PrivacyFragmentUploader</c> then emits the empty-fragment
/// sentinel so the archive assembler records the provider in <c>manifest.EmptyProviders</c>
/// rather than failing the whole export.
/// </remarks>
public sealed class IdentityLocalPrivacyDataProvider(UserManager<GranitUser> userManager) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "identity-local";

    /// <inheritdoc />
    public static string ContentType => "application/json";

    /// <inheritdoc />
    public static string FileName(Guid requestId) => "identity-local.json";

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        GranitUser? user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);

        IdentityLocalExportDto dto = new(
            Id: user.Id,
            UserName: user.UserName,
            Email: user.Email,
            EmailConfirmed: user.EmailConfirmed,
            FirstName: user.FirstName,
            LastName: user.LastName,
            PhoneNumber: user.PhoneNumber,
            PhoneNumberConfirmed: user.PhoneNumberConfirmed,
            TwoFactorEnabled: user.TwoFactorEnabled,
            LockoutEnabled: user.LockoutEnabled,
            LockoutEnd: user.LockoutEnd,
            AccessFailedCount: user.AccessFailedCount,
            TenantId: user.TenantId,
            CreatedAt: user.CreatedAt,
            CreatedBy: user.CreatedBy,
            ModifiedAt: user.ModifiedAt,
            ModifiedBy: user.ModifiedBy,
            IsDeleted: user.IsDeleted,
            DeletedAt: user.DeletedAt,
            CustomAttributesJson: user.CustomAttributesJson,
            Roles: roles);

        return JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

/// <summary>Data transfer record written to the <c>identity-local.json</c> fragment.</summary>
internal sealed record IdentityLocalExportDto(
    Guid Id,
    string? UserName,
    string? Email,
    bool EmailConfirmed,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    bool TwoFactorEnabled,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    int AccessFailedCount,
    Guid? TenantId,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    string? CustomAttributesJson,
    IList<string> Roles);
