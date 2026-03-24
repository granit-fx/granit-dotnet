using Granit.Modularity;

namespace Granit.Imaging;

/// <summary>
/// Granit module for image processing abstractions.
/// </summary>
/// <remarks>
/// This is an anchor module with no service registration. It declares the
/// <c>Granit.Imaging</c> package in the module dependency graph.
/// <para>
/// Concrete implementations must depend on this module:
/// <list type="bullet">
///   <item><c>Granit.Imaging.MagickNet</c> — Magick.NET (Apache 2.0) implementation</item>
/// </list>
/// </para>
/// </remarks>
public sealed class GranitImagingModule : GranitModule;
