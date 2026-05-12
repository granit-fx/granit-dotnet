using System.Collections.Generic;

namespace Granit.Documents.Renditions.Wolverine.Policies;

/// <summary>
/// Decides which <see cref="RenditionTarget"/>s to schedule for a freshly uploaded
/// document version. Resolved as a singleton; the default registration is
/// <see cref="DefaultRenditionTypePolicy"/> — hosts override by registering their own
/// implementation before <c>AddGranitDocumentsRenditionsWolverine</c>.
/// </summary>
public interface IRenditionTypePolicy
{
    /// <summary>
    /// Returns the rendition targets to generate for the given source MIME type. An
    /// empty result short-circuits the event handler with no further work.
    /// </summary>
    IReadOnlyList<RenditionTarget> ResolveTargets(string sourceContentType);
}
