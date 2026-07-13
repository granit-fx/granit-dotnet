using Granit.Privacy.LegalAgreements.Domain;

namespace Granit.Privacy.LegalAgreements.Internal;

/// <summary>
/// Checks consent by comparing the stored version with the current version in the registry.
/// </summary>
internal sealed class LegalAgreementChecker(
    ILegalDocumentRegistry documentRegistry,
    ILegalAgreementStoreReader storeReader) : ILegalAgreementChecker
{
    /// <inheritdoc/>
    public async Task<bool> HasAcceptedLatestAsync(Guid userId, string documentId, CancellationToken cancellationToken = default)
    {
        LegalDocumentDefinition? definition = await documentRegistry
            .GetDefinitionAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return false;
        }

        LegalAgreementBase? latest = await storeReader.FindLatestAsync(userId, documentId, cancellationToken).ConfigureAwait(false);
        if (latest is null)
        {
            return false;
        }

        return string.Equals(latest.Version, definition.CurrentVersion, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LegalAgreementBase>> GetUserAgreementsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        storeReader.FindAllByUserAsync(userId, cancellationToken);
}
