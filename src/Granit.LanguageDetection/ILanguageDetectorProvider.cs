namespace Granit.LanguageDetection;

/// <summary>
/// Marker interface for concrete <see cref="ILanguageDetector"/> implementations that
/// participate in the <see cref="CompositeLanguageDetector"/> priority chain. Every
/// concrete provider — the bundled trigram detector, AI-backed detectors, metadata-
/// hint detectors — registers under this interface (typically via
/// <c>services.TryAddEnumerable(ServiceDescriptor.Singleton&lt;ILanguageDetectorProvider, ConcreteDetector&gt;(...))</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a marker.</b> If concrete providers register under <see cref="ILanguageDetector"/>
/// directly, the standard ASP.NET Core DI container resolves
/// <c>sp.GetRequiredService&lt;ILanguageDetector&gt;()</c> to the LAST descriptor for
/// that service — which is whichever provider was registered last, not the composite.
/// The chain is silently bypassed. Splitting providers (this interface) from the
/// consumer-facing facade (<see cref="ILanguageDetector"/>) keeps the two resolutions
/// independent: the composite is always the single <see cref="ILanguageDetector"/>,
/// providers fan in via <c>IEnumerable&lt;ILanguageDetectorProvider&gt;</c>.
/// </para>
/// <para>
/// <b>Implementing a custom detector.</b> Implement this interface (it inherits
/// <see cref="ILanguageDetector"/>, so a single class satisfies both contracts), then
/// register it with <c>TryAddEnumerable</c>.
/// </para>
/// </remarks>
public interface ILanguageDetectorProvider : ILanguageDetector;
