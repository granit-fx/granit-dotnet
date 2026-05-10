using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Events;
using Granit.Documents.Renditions.BackgroundJobs.Policies;
using Microsoft.Extensions.Logging;

namespace Granit.Documents.Renditions.BackgroundJobs.Handlers;

/// <summary>
/// Wolverine handler subscribed to <see cref="DocumentVersionAddedEvent"/>. Resolves
/// the rendition targets via <see cref="IRenditionTypePolicy"/> and delegates the
/// per-rendition generation to <see cref="RenditionGenerationService"/>.
/// </summary>
public partial class DocumentVersionAddedRenditionsHandler
{
    /// <summary>Wolverine-style handler entry point. Public + static per framework convention.</summary>
    public static async Task HandleAsync(
        DocumentVersionAddedEvent evt,
        IRenditionTypePolicy policy,
        IRenditionGenerationService generationService,
        ILogger<DocumentVersionAddedRenditionsHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        IReadOnlyList<RenditionTarget> targets = policy.ResolveTargets(evt.ContentType);
        if (targets.Count == 0)
        {
            LogNoTargets(logger, evt.VersionId, evt.ContentType);
            return;
        }

        foreach (RenditionTarget target in targets)
        {
            await generationService.GenerateAsync(
                evt.DocumentId,
                evt.TenantId,
                evt.VersionId,
                evt.BlobDescriptorId,
                evt.ContentType,
                target,
                cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "No rendition targets registered for source content type '{ContentType}' (version {VersionId}); skipping.")]
    private static partial void LogNoTargets(ILogger logger, Guid versionId, string contentType);
}
