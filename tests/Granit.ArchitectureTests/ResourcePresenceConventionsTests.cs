using System.Reflection;
using System.Text.RegularExpressions;
using Granit.Presence.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Conventions for the resource-scoped presence rooms surface introduced in #2408. Keeps the
/// base <c>Granit.Presence</c> package free of HTTP / EF concerns and verifies that the new
/// abstractions live in the canonical namespace.
/// </summary>
public sealed partial class ResourcePresenceConventionsTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void IResourcePresenceTracker_lives_in_abstractions_namespace() =>
        typeof(IResourcePresenceTracker).Namespace.ShouldBe("Granit.Presence.Abstractions");

    [Fact]
    public void IResourcePresenceVisibilityPolicy_lives_in_abstractions_namespace() =>
        typeof(IResourcePresenceVisibilityPolicy).Namespace.ShouldBe("Granit.Presence.Abstractions");

    [Fact]
    public void ResourceRef_lives_in_abstractions_namespace() =>
        typeof(ResourceRef).Namespace.ShouldBe("Granit.Presence.Abstractions");

    [Fact]
    public void FusionCacheResourcePresenceTracker_is_internal_sealed()
    {
        Assembly presenceAssembly = typeof(IResourcePresenceTracker).Assembly;
        Type? type = presenceAssembly.GetType("Granit.Presence.Internal.FusionCacheResourcePresenceTracker");
        type.ShouldNotBeNull("FusionCacheResourcePresenceTracker type must exist in Granit.Presence.");
        type!.IsSealed.ShouldBeTrue("FusionCacheResourcePresenceTracker must be sealed.");
        type.IsVisible.ShouldBeFalse("FusionCacheResourcePresenceTracker must not be visible outside the assembly.");
    }

    [Fact]
    public void Granit_Presence_base_module_does_not_reference_EntityFrameworkCore()
    {
        string csproj = Path.Join(RepoRoot, "src", "Granit.Presence", "Granit.Presence.csproj");
        File.Exists(csproj).ShouldBeTrue();

        string content = File.ReadAllText(csproj);
        AnyReferenceMatching(content, "Microsoft.EntityFrameworkCore")
            .ShouldBeFalse("Granit.Presence (base module) must not depend on EF Core.");
    }

    [Fact]
    public void Granit_Presence_base_module_does_not_reference_AspNetCore()
    {
        string csproj = Path.Join(RepoRoot, "src", "Granit.Presence", "Granit.Presence.csproj");
        File.Exists(csproj).ShouldBeTrue();

        string content = File.ReadAllText(csproj);

        AnyReferenceMatching(content, "Microsoft.AspNetCore")
            .ShouldBeFalse("Granit.Presence (base module) must not depend on ASP.NET Core.");

        FrameworkReferenceAspNetCore().IsMatch(content)
            .ShouldBeFalse("Granit.Presence must not declare a FrameworkReference on Microsoft.AspNetCore.App.");
    }

    private static bool AnyReferenceMatching(string csproj, string prefix)
    {
        foreach (Match m in PackageReferenceInclude().Matches(csproj))
        {
            if (m.Groups[1].Value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"<PackageReference\s+Include\s*=\s*""([^""]+)""")]
    private static partial Regex PackageReferenceInclude();

    [GeneratedRegex(@"<FrameworkReference\s+Include\s*=\s*""Microsoft\.AspNetCore\.App""")]
    private static partial Regex FrameworkReferenceAspNetCore();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ResourcePresenceConventionsTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not locate repository root (no .git found).");
    }
}
