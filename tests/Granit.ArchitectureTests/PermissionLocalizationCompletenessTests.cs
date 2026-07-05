using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Companion to <c>MetricLocalizationCompletenessTests</c> and
/// <c>DashboardLocalizationCompletenessTests</c> — extends the same
/// 15-base-cultures completeness rule to permission constants. Every
/// <c>public const string Name = "Group.Resource.Action";</c> in a module's
/// <c>*Permissions.cs</c> file MUST have a <c>Permission:{value}</c> resource
/// key in all 15 base cultures of that module's <c>Localization/</c> directory.
/// Regional variants (<c>fr-CA</c>, <c>en-GB</c>, <c>pt-BR</c>) are exempt:
/// they ship only differing keys and fall through to their parent culture.
/// </summary>
/// <remarks>
/// <para>
/// The failure aggregates every (permission, culture) pair missing a key, so a
/// developer adding a new permission without a complete translation pack sees
/// the entire list at once instead of one-failure-per-culture spam.
/// </para>
/// <para>
/// <see cref="PairingExemptions"/> is not used here — permissions are always
/// user-facing and there is no equivalent of an <c>[INFRA]</c> exemption (an
/// internal permission that nobody will ever see translated). If a permission
/// surfaces in the admin UI, it surfaces in every supported culture.
/// </para>
/// </remarks>
public sealed partial class PermissionLocalizationCompletenessTests
{
    private static readonly string[] BaseCultures =
    [
        "en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja",
        "pl", "tr", "ko", "sv", "cs", "hi",
    ];

    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_permission_constant_should_have_localization_keys_in_all_base_cultures()
    {
        IReadOnlyList<PermissionDescriptor> permissions = ScanPermissions();
        permissions.ShouldNotBeEmpty(
            "No permissions discovered — the test cannot run. " +
            "Ensure at least one *.Endpoints module ships a *Permissions.cs file with a permission constant.");

        List<string> missing = [];

        foreach (PermissionDescriptor permission in permissions)
        {
            string moduleSrcDir = Path.Join(RepoRoot, "src", permission.OwningModuleDir);
            string localizationDir = Path.Join(moduleSrcDir, "Localization");

            if (!Directory.Exists(localizationDir))
            {
                missing.Add(
                    $"{permission.Value} (declared in {permission.OwningModuleDir}): " +
                    $"Localization/ directory not found at {Path.GetRelativePath(RepoRoot, localizationDir)}");
                continue;
            }

            string resourceKey = $"Permission:{permission.Value}";

            foreach (string culture in BaseCultures)
            {
                bool found = LocateKeyInCultureFiles(localizationDir, culture, resourceKey);
                if (!found)
                {
                    missing.Add(
                        $"{permission.Value} -> {culture} " +
                        $"(expected key '{resourceKey}' in src/{permission.OwningModuleDir}/Localization/**/{culture}.json)");
                }
            }
        }

        missing.ShouldBeEmpty(
            "Every permission constant must have a 'Permission:{Value}' key in all 15 base cultures " +
            $"of its owning module. {missing.Count} key(s) missing:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    private static bool LocateKeyInCultureFiles(string localizationDir, string culture, string resourceKey)
    {
        string[] candidateFiles = Directory.GetFiles(localizationDir, $"{culture}.json", SearchOption.AllDirectories);
        return candidateFiles.Any(file => FileContainsKey(file, resourceKey));
    }

    private static bool FileContainsKey(string filePath, string key)
    {
        using FileStream stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Granit localization files use either a flat root object ({ "Key": "Value" })
        // or a nested layout with the culture envelope ({ "culture": "...", "texts": { "Key": "Value" } }).
        if (root.TryGetProperty("texts", out JsonElement texts)
            && texts.ValueKind == JsonValueKind.Object
            && texts.TryGetProperty(key, out _))
        {
            return true;
        }

        return root.TryGetProperty(key, out _);
    }

    private static List<PermissionDescriptor> ScanPermissions()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<PermissionDescriptor> permissions = [];

        foreach (string permissionFile in EnumeratePermissionFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(srcDir, permissionFile);
            string moduleDir = relativePath.Split(Path.DirectorySeparatorChar)[0];

            foreach ((string name, string value) in EnumeratePermissionConstants(permissionFile))
            {
                if (name == "GroupName")
                {
                    continue;
                }

                permissions.Add(new PermissionDescriptor(value, moduleDir));
            }
        }

        return permissions;
    }

    private static IEnumerable<string> EnumeratePermissionFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*Permissions.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];
            if (!moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static IEnumerable<(string Name, string Value)> EnumeratePermissionConstants(string file)
    {
        foreach (string line in File.ReadLines(file))
        {
            Match match = PermissionConstant().Match(line);
            if (match.Success)
            {
                yield return (match.Groups[1].Value, match.Groups[2].Value);
            }
        }
    }

    [GeneratedRegex(@"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex PermissionConstant();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(PermissionLocalizationCompletenessTests).Assembly.Location);
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

    private sealed record PermissionDescriptor(string Value, string OwningModuleDir);
}
