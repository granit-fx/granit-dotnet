using Granit.Auditing.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.Auditing.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the auditing privacy provider.
/// </summary>
public static class PrivacyBuilderAuditingExtensions
{
    /// <summary>
    /// Registers <see cref="AuditingPrivacyDataProvider"/> as an <c>IPrivacyDataProvider</c>
    /// and adds <c>"auditing"</c> to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. This call also wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure the provider depends on
    /// (<c>IStagedFragmentBuilder</c>, uploader, assembler) — registration is idempotent
    /// (<c>TryAdd</c>), so the host no longer needs an explicit
    /// <c>[DependsOn(GranitPrivacyBlobStorageModule)]</c>. The provider still requires
    /// <c>GranitAuditingModule</c> (for <c>IAuditingReader</c>) to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitAuditingPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<AuditingPrivacyDataProvider>();
    }
}
