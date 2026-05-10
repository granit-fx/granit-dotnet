namespace Granit.Documents.Renditions.Domain;

/// <summary>Lifecycle of a <see cref="DocumentRendition"/> row.</summary>
public enum RenditionStatus
{
    /// <summary>Row created, generation not yet started — typically inserted by the background job dispatcher.</summary>
    Pending = 0,

    /// <summary>Provider pipeline is running.</summary>
    Generating = 1,

    /// <summary>Rendition produced and persisted; the <c>BlobDescriptorId</c> column points at the bytes.</summary>
    Ready = 2,

    /// <summary>Generation failed terminally (after retry budget); see the parent <c>RenditionFailedEvent</c> for the reason.</summary>
    Failed = 3,
}
