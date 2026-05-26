using Granit.DataProtection;
using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport;

/// <summary>
/// A fragment received from a data provider during the privacy export saga. Carries the
/// pointer the assembler needs to materialise the entry in the final archive plus the
/// HMAC capability the assembler verifies before opening the source stream.
/// </summary>
/// <param name="ProviderName">Originating <see cref="IPrivacyDataProvider.ProviderName"/>.</param>
/// <param name="FragmentKind"><c>"staged"</c> when the fragment bytes live in the staging
/// container; <c>"passthrough"</c> when the fragment points at an already-persisted source
/// blob (single-transit).</param>
/// <param name="SourceContainer">Container holding the underlying blob (staging container
/// for <c>staged</c>, source container for <c>passthrough</c>).</param>
/// <param name="BlobReferenceId">Reference to the underlying blob.</param>
/// <param name="EntryPath">Path the fragment will occupy in the final archive (already
/// sanitised by the producing provider / builder).</param>
/// <param name="ContentType">MIME type of the entry — drives compression mode at assembly.</param>
/// <param name="IntegrityTag">HMAC capability tag bound to the fragment identity.
/// Verified by the assembler before any blob read.</param>
public sealed record ReceivedFragment(
    string ProviderName,
    string FragmentKind,
    string SourceContainer,
    BlobReference BlobReferenceId,
    [property: SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)]
    string EntryPath,
    string ContentType,
    string IntegrityTag);
