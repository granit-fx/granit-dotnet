namespace Granit.Entities.Layouts;

/// <summary>
/// Closed enumeration of card sizes a gallery layout may declare. Drives the
/// CSS-grid track sizing in the front-end <c>EntityGallery</c> renderer; the
/// server merely surfaces the picked value in the manifest.
/// </summary>
public enum GalleryCardSize
{
    /// <summary>Compact tile — high density, smaller thumbnails.</summary>
    Small,

    /// <summary>Default tile — balanced density vs preview clarity.</summary>
    Medium,

    /// <summary>Showcase tile — large preview area, lower density.</summary>
    Large,
}
