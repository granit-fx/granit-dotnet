using System.Runtime.CompilerServices;
using Granit.Identity.Local.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.Privacy.DataExport;

/// <summary>
/// Privacy data provider for <c>Granit.Identity.Local</c>. Emits the authenticated user's
/// profile (<see cref="LocalIdentity"/>) and the roles they belong to as a single staged
/// JSON fragment during the scatter-gather personal-data export saga (GDPR Art. 15 / 20).
/// </summary>
/// <remarks>
/// Yields nothing when the user has been hard-deleted or was never provisioned locally —
/// the uploader then emits the empty-fragment sentinel so the assembler records the
/// provider in <c>EmptyProviders</c> rather than failing the export.
/// </remarks>
public sealed class IdentityLocalPrivacyDataProvider(
    UserManager<LocalIdentity> userManager,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "identity-local";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.IdentityLocal";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        LocalIdentity? user = await userManager.FindByIdAsync(context.SubjectUserId.ToString()).ConfigureAwait(false);
        return user is not null;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        LocalIdentity? user = await userManager.FindByIdAsync(context.SubjectUserId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            yield break;
        }

        IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);

        // CreatedBy / ModifiedBy intentionally omitted — those columns store administrator
        // user identifiers, which are third-party personal data and would expose admin
        // identities to the data subject (GDPR Art. 5(1)(c) — data minimisation).
        var dto = new IdentityLocalExportResponse(
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
            ModifiedAt: user.ModifiedAt,
            IsDeleted: user.IsDeleted,
            DeletedAt: user.DeletedAt,
            CustomAttributesJson: user.CustomAttributesJson,
            Roles: roles);

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "identity-local.json", dto, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Export record written to the <c>identity-local.json</c> fragment.
/// Excludes the audit-trail attribution fields (<c>CreatedBy</c> / <c>ModifiedBy</c>)
/// so administrator user identifiers — third-party personal data — do not leak via
/// the data subject's GDPR Art. 15 export.
/// </summary>
internal sealed record IdentityLocalExportResponse(
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
    DateTimeOffset? ModifiedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    string? CustomAttributesJson,
    IList<string> Roles);
