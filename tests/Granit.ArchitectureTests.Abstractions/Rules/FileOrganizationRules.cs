using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable file organization rules: types with well-known suffixes must reside
/// in the corresponding subfolder within their module.
/// </summary>
public static partial class FileOrganizationRules
{
    /// <summary>Module classes must be at the module root, not in subfolders.</summary>
    /// <param name="srcDir">Path to <c>src/</c>.</param>
    /// <param name="moduleFilePrefix">File name prefix for module files (e.g. <c>"Granit"</c>, <c>"Showcase"</c>).</param>
    /// <param name="exemptFileNames">File names (without .cs) that are intentionally elsewhere.</param>
    public static void ModuleClassesShouldBeAtModuleRoot(
        string srcDir,
        string moduleFilePrefix = "",
        params string[] exemptFileNames)
    {
        HashSet<string> exempt = new(exemptFileNames, StringComparer.OrdinalIgnoreCase);
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string fileName = Path.GetFileName(csFile);
            if (!fileName.EndsWith("Module.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (moduleFilePrefix.Length > 0 && !fileName.StartsWith(moduleFilePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string nameWithoutExt = Path.GetFileNameWithoutExtension(csFile);
            if (exempt.Contains(nameWithoutExt))
            {
                continue;
            }

            if (!IsAtModuleRoot(srcDir, csFile))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            $"Module classes (*Module.cs) must be at the module root directory. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Extension classes (*Extensions.cs) should not be at the module root.</summary>
    public static void ExtensionClassesShouldNotBeAtModuleRoot(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Extensions.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsAtModuleRoot(srcDir, csFile))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Extension classes should be in an Extensions/ subfolder, not at the module root. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Exception classes (*Exception.cs) must be in an Exceptions/ subfolder.</summary>
    public static void ExceptionClassesShouldResideInExceptionsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Exception.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Exceptions"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Exception classes must reside in an Exceptions/ subfolder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Options classes (*Options.cs) should not be at the module root.</summary>
    public static void OptionsClassesShouldNotBeAtModuleRoot(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Options.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsAtModuleRoot(srcDir, csFile))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Options classes should be in an Options/ or Internal/ subfolder, not at the module root. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>In *.Endpoints modules, endpoint classes (*Endpoints.cs) must be in Endpoints/ or Internal/.</summary>
    public static void EndpointClassesShouldResideInEndpointsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Endpoints.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!GetModuleName(srcDir, csFile).EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Endpoints") && !IsInFolder(csFile, "Internal"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Endpoint classes in *.Endpoints modules must reside in Endpoints/ or Internal/. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>In *.Endpoints modules, DTO classes (*Request.cs, *Response.cs) must be in Dtos/.</summary>
    public static void DtoClassesShouldResideInDtosFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string fileName = Path.GetFileName(csFile);
            if (!fileName.EndsWith("Request.cs", StringComparison.Ordinal)
                && !fileName.EndsWith("Response.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!GetModuleName(srcDir, csFile).EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Dtos"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "DTO classes (*Request.cs, *Response.cs) in *.Endpoints modules must reside in Dtos/. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>In *.EntityFrameworkCore modules, *Configuration.cs must be in Configurations/ or Internal/.</summary>
    public static void EfCoreConfigurationsShouldResideInConfigurationsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Configuration.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!GetModuleName(srcDir, csFile).Contains("EntityFrameworkCore", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Configurations")
                && !IsInFolder(csFile, "EntityConfigurations")
                && !IsInFolder(csFile, "Internal"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "EF Core configuration classes must reside in Configurations/, EntityConfigurations/, or Internal/. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Validator classes in *.Endpoints modules must be in Validators/ or Internal/.</summary>
    public static void EndpointValidatorsShouldResideInValidatorsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Validator.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!GetModuleName(srcDir, csFile).EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Validators") && !IsInFolder(csFile, "Internal"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Validator classes in *.Endpoints modules must reside in Validators/ or Internal/. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>In *.Endpoints modules, permission files (*Permissions.cs, *PermissionDefinitionProvider.cs) must be in Permissions/.</summary>
    public static void PermissionDefinitionsShouldResideInPermissionsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string fileName = Path.GetFileName(csFile);
            if (!fileName.EndsWith("Permissions.cs", StringComparison.Ordinal)
                && !fileName.EndsWith("PermissionDefinitionProvider.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!GetModuleName(srcDir, csFile).EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Permissions"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Permission definition classes in *.Endpoints modules must reside in Permissions/. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Metrics classes (*Metrics.cs) must reside in a Diagnostics/ subfolder.</summary>
    public static void MetricsClassesShouldResideInDiagnosticsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("Metrics.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Diagnostics"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "Metrics classes (*Metrics.cs) must reside in a Diagnostics/ subfolder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>ActivitySource classes (*ActivitySource.cs) must reside in a Diagnostics/ subfolder.</summary>
    public static void ActivitySourceClassesShouldResideInDiagnosticsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (!Path.GetFileName(csFile).EndsWith("ActivitySource.cs", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Diagnostics"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "ActivitySource classes (*ActivitySource.cs) must reside in a Diagnostics/ subfolder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>DbContext classes in *.EntityFrameworkCore packages must reside in Internal/.</summary>
    public static void DbContextClassesShouldResideInInternalFolder(
        string srcDir,
        params string[] exemptFileNames)
    {
        HashSet<string> exempt = new(exemptFileNames, StringComparer.OrdinalIgnoreCase)
        {
            "GranitDbContext", // public abstract base class
        };
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string fileName = Path.GetFileName(csFile);
            if (!fileName.EndsWith("DbContext.cs", StringComparison.Ordinal) || fileName.StartsWith('I'))
            {
                continue;
            }

            if (exempt.Contains(Path.GetFileNameWithoutExtension(csFile)))
            {
                continue;
            }

            string moduleName = GetModuleName(srcDir, csFile);
            if (!moduleName.Contains("EntityFrameworkCore", StringComparison.Ordinal)
                && !moduleName.Contains("Migrations", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsInFolder(csFile, "Internal"))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "DbContext classes in *.EntityFrameworkCore packages must reside in an Internal/ subfolder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Types inheriting from domain base classes in non-EfCore packages must reside in Domain/.
    /// Types in *.EntityFrameworkCore packages must reside in Entities/ or Internal/.
    /// </summary>
    public static void DomainTypesShouldResideInCorrectFolder(
        string srcDir,
        params string[] exemptModuleNames)
    {
        HashSet<string> exempt = new(exemptModuleNames, StringComparer.OrdinalIgnoreCase);
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string moduleName = GetModuleName(srcDir, csFile);

            if (exempt.Contains(moduleName))
            {
                continue;
            }

            bool isEfCore = moduleName.Contains("EntityFrameworkCore", StringComparison.Ordinal);

            if (isEfCore)
            {
                if (!IsInFolder(csFile, "Entities") && !IsInFolder(csFile, "Internal")
                    && InheritsFromDomainBaseClass(csFile))
                {
                    violations.Add($"[EfCore] {Path.GetRelativePath(srcDir, csFile)} → must be in Entities/ or Internal/");
                }
            }
            else
            {
                if (!IsInFolder(csFile, "Domain") && InheritsFromDomainBaseClass(csFile))
                {
                    violations.Add($"[Base] {Path.GetRelativePath(srcDir, csFile)} → must be in Domain/");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Domain types must reside in Domain/ (base modules) or Entities/Internal/ (EfCore modules). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    // ── Shared helpers ────────────────────────────────────────────────────────────

    private static IEnumerable<string> GetSrcCsFiles(string srcDir) =>
        Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

    private static bool IsAtModuleRoot(string srcDir, string filePath)
    {
        string relativePath = Path.GetRelativePath(srcDir, filePath);
        return relativePath.Split(Path.DirectorySeparatorChar).Length == 2;
    }

    private static bool IsInFolder(string filePath, string folderName)
    {
        string sep = Path.DirectorySeparatorChar.ToString();
        return filePath.Contains(sep + folderName + sep, StringComparison.Ordinal);
    }

    private static string GetModuleName(string srcDir, string filePath)
    {
        string relativePath = Path.GetRelativePath(srcDir, filePath);
        int sepIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
        return sepIndex >= 0 ? relativePath[..sepIndex] : relativePath;
    }

    private static bool InheritsFromDomainBaseClass(string filePath)
    {
        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith('*'))
            {
                continue;
            }

            if ((line.Contains("class ") || line.Contains("record ")) && DomainBaseClassInheritance().IsMatch(line))
            {
                return true;
            }
        }
        return false;
    }

    [GeneratedRegex(
        @":\s*(FullAuditedAggregateRoot|AuditedAggregateRoot|CreationAuditedAggregateRoot|AggregateRoot|" +
        @"FullAuditedEntity|AuditedEntity|CreationAuditedEntity|Entity|ValueObject|AuditedTranslation|Translation)\b",
        RegexOptions.Multiline)]
    private static partial Regex DomainBaseClassInheritance();
}
