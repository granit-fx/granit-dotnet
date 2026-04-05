using Granit.Privacy.LegalAgreements.Events;
using Granit.Privacy.LegalAgreements.Internal;

namespace Granit.Privacy.LegalAgreements;

/// <summary>
/// Refreshes the <see cref="CompositeLegalDocumentRegistry"/> cache when a legal document
/// is published or archived. Ensures all pods stay coherent in multi-instance deployments.
/// </summary>
public class LegalDocumentCacheInvalidatedHandler
{
    public static async Task HandleAsync(
        LegalDocumentCacheInvalidatedEto evt,
        ILegalDocumentRegistry registry,
        CancellationToken cancellationToken)
    {
        if (registry is CompositeLegalDocumentRegistry composite)
        {
            await composite.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
