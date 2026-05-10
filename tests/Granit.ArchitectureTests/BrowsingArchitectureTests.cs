using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules pinning the <c>Granit.Browsing</c> abstraction-then-provider
/// shape (epic #1936). Mirrors the discipline already enforced on <c>Granit.Imaging</c>
/// and <c>Granit.BlobStorage</c>.
/// </summary>
public sealed class BrowsingArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Only the PuppeteerSharp provider package and its test harness may reference the
    /// <c>PuppeteerSharp</c> NuGet directly. Every other consumer (e.g.
    /// <c>Granit.DocumentGeneration.Pdf</c>) routes through the <c>Granit.Browsing</c>
    /// abstraction. Pinning this rule prevents the historical
    /// "everyone imports PuppeteerSharp" anti-pattern from creeping back.
    /// </summary>
    [Fact]
    public void Only_Granit_Browsing_PuppeteerSharp_should_reference_PuppeteerSharp_directly() =>
        AssertExclusiveProviderReference(
            packageId: "PuppeteerSharp",
            allowedProjects:
            [
                "Granit.Browsing.PuppeteerSharp",
                "Granit.Browsing.PuppeteerSharp.Tests",
            ]);

    /// <summary>
    /// Only the Playwright provider package and its test harness may reference the
    /// <c>Microsoft.Playwright</c> NuGet directly.
    /// </summary>
    [Fact]
    public void Only_Granit_Browsing_Playwright_should_reference_Microsoft_Playwright_directly() =>
        AssertExclusiveProviderReference(
            packageId: "Microsoft.Playwright",
            allowedProjects:
            [
                "Granit.Browsing.Playwright",
                "Granit.Browsing.Playwright.Tests",
            ]);

    /// <summary>
    /// The base <c>Granit.Browsing</c> package must not reference any provider impl —
    /// it would invert the abstraction-then-provider direction and force every consumer
    /// to drag the provider's dependencies (Chromium binaries) into the build graph.
    /// </summary>
    [Fact]
    public void Granit_Browsing_base_should_not_reference_provider_packages()
    {
        string csprojPath = Path.Join(
            RepoRoot, "src", "Granit.Browsing", "Granit.Browsing.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        IEnumerable<string> projectRefs = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);

        IEnumerable<string> providerRefs = projectRefs
            .Where(r => r.Contains("Granit.Browsing.PuppeteerSharp", StringComparison.Ordinal)
                     || r.Contains("Granit.Browsing.Playwright", StringComparison.Ordinal));

        providerRefs.ShouldBeEmpty(
            "Granit.Browsing (base abstraction) must not reference provider impls. " +
            "Move the consumer to the provider package or invert the dependency.");
    }

    /// <summary>
    /// Every <c>Granit.Browsing.*</c> provider package must reference the base
    /// <c>Granit.Browsing</c> contract. Without this reference, the provider is
    /// implementing a parallel surface and consumers can't hot-swap providers.
    /// </summary>
    [Theory]
    [InlineData("Granit.Browsing.PuppeteerSharp")]
    [InlineData("Granit.Browsing.Playwright")]
    public void Provider_package_should_reference_Granit_Browsing(string providerProjectName)
    {
        string csprojPath = Path.Join(
            RepoRoot, "src", providerProjectName, $"{providerProjectName}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        bool referencesBase = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Any(r => r.Contains("Granit.Browsing.csproj", StringComparison.Ordinal)
                  || r.EndsWith("/Granit.Browsing/Granit.Browsing.csproj", StringComparison.Ordinal)
                  || r.EndsWith(@"\Granit.Browsing\Granit.Browsing.csproj", StringComparison.Ordinal));

        referencesBase.ShouldBeTrue(
            $"{providerProjectName} must reference the base Granit.Browsing contract.");
    }

    /// <summary>
    /// Every public type in <c>Granit.Browsing.Capabilities</c> whose name ends with
    /// <c>Capability</c> must be an interface. The convention keeps capability discovery
    /// uniform — consumers inject <c>I*Capability</c>, providers implement them.
    /// </summary>
    [Fact]
    public void Capability_named_types_in_Granit_Browsing_Capabilities_should_be_interfaces()
    {
        System.Reflection.Assembly browsingAssembly = typeof(Granit.Browsing.IHeadlessBrowser).Assembly;
        IEnumerable<Type> capabilityTypes = browsingAssembly.GetExportedTypes()
            .Where(t => t.Namespace == "Granit.Browsing.Capabilities"
                     && t.Name.EndsWith("Capability", StringComparison.Ordinal));

        IEnumerable<string> nonInterfaces = capabilityTypes
            .Where(t => !t.IsInterface)
            .Select(t => t.FullName!);

        nonInterfaces.ShouldBeEmpty(
            "Types ending in 'Capability' in Granit.Browsing.Capabilities must be interfaces. " +
            "Move concrete impls to providers (Granit.Browsing.PuppeteerSharp.Internal, " +
            "Granit.Browsing.Playwright.Internal) and rename the offending types.");
    }

    private static void AssertExclusiveProviderReference(
        string packageId,
        IReadOnlyList<string> allowedProjects)
    {
        List<string> violators = [];

        foreach (string csproj in EnumerateRepoCsproj())
        {
            string projectName = Path.GetFileNameWithoutExtension(csproj);
            if (allowedProjects.Contains(projectName))
            {
                continue;
            }

            var doc = XDocument.Load(csproj);
            bool references = doc.Descendants("PackageReference")
                .Any(e => string.Equals(
                    e.Attribute("Include")?.Value, packageId, StringComparison.Ordinal));

            if (references)
            {
                violators.Add(projectName);
            }
        }

        violators.ShouldBeEmpty(
            $"Only {string.Join(" / ", allowedProjects)} may reference the {packageId} NuGet directly. " +
            $"Other consumers must route through Granit.Browsing's IHeadlessBrowser + capability interfaces. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    private static IEnumerable<string> EnumerateRepoCsproj()
    {
        foreach (string root in (string[])["src", "tests"])
        {
            string dir = Path.Join(RepoRoot, root);
            if (!Directory.Exists(dir))
            {
                continue;
            }
            foreach (string csproj in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories))
            {
                yield return csproj;
            }
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(BrowsingArchitectureTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }
}
