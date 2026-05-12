using System;
using System.Collections.Generic;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Options;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.Wolverine.Policies;

/// <summary>
/// Default <see cref="IRenditionTypePolicy"/> mapping each source MIME family to a
/// canonical rendition set:
/// <list type="bullet">
///   <item><c>image/*</c> → <see cref="RenditionType.Thumbnail"/> + <see cref="RenditionType.Web"/></item>
///   <item><c>application/pdf</c> → <see cref="RenditionType.Thumbnail"/></item>
///   <item>office MIMEs (docx/xlsx/pptx + legacy doc/xls/ppt) → <see cref="RenditionType.Thumbnail"/></item>
/// </list>
/// </summary>
public sealed class DefaultRenditionTypePolicy(IOptions<GranitRenditionsOptions> options) : IRenditionTypePolicy
{
    private static readonly HashSet<string> OfficeMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/msword",
        "application/vnd.ms-excel",
        "application/vnd.ms-powerpoint",
    };

    /// <inheritdoc />
    public IReadOnlyList<RenditionTarget> ResolveTargets(string sourceContentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);

        ThumbnailDefaults thumb = options.Value.Thumbnail;
        RenditionTarget thumbnail = new(
            RenditionType.Thumbnail,
            thumb.Format,
            new RenditionDimensions(thumb.Width, thumb.Height),
            thumb.Quality);

        if (sourceContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            RenditionTarget web = new(RenditionType.Web, "image/webp");
            return [thumbnail, web];
        }

        if (string.Equals(sourceContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return [thumbnail];
        }

        if (OfficeMimes.Contains(sourceContentType))
        {
            return [thumbnail];
        }

        return [];
    }
}
