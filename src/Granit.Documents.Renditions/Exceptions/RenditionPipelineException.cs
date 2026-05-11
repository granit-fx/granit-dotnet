using System;

namespace Granit.Documents.Renditions.Exceptions;

/// <summary>
/// Thrown by <see cref="IRenditionPipeline"/> when no provider chain bridges the
/// requested <c>(sourceContentType, target)</c> pair within the configured maximum chain
/// length, or when an underlying provider raises a terminal failure.
/// </summary>
public sealed class RenditionPipelineException : Exception
{
    /// <summary>Creates a new <see cref="RenditionPipelineException"/>.</summary>
    public RenditionPipelineException(string message) : base(message)
    {
    }

    /// <summary>Creates a new <see cref="RenditionPipelineException"/> wrapping <paramref name="innerException"/>.</summary>
    public RenditionPipelineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
