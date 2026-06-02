using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces file organization conventions: types with well-known suffixes
/// must reside in the corresponding subfolder within their module.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class FileOrganizationTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(FileOrganizationTests).Assembly);
    private static readonly string SrcDir = Path.Join(RepoRoot, "src");

    [Fact]
    public void Module_classes_should_be_at_module_root() =>
        FileOrganizationRules.ModuleClassesShouldBeAtModuleRoot(SrcDir, moduleFilePrefix: "Granit",
            // Base class lives in Granit/Modularity/ by design — not a module instance
            "GranitModule");

    [Fact]
    public void Extension_classes_should_not_be_at_module_root() =>
        FileOrganizationRules.ExtensionClassesShouldNotBeAtModuleRoot(SrcDir);

    [Fact]
    public void Exception_classes_should_reside_in_Exceptions_folder() =>
        FileOrganizationRules.ExceptionClassesShouldResideInExceptionsFolder(SrcDir);

    [Fact]
    public void Options_classes_should_not_be_at_module_root() =>
        FileOrganizationRules.OptionsClassesShouldNotBeAtModuleRoot(SrcDir);

    [Fact]
    public void Endpoint_classes_should_reside_in_Endpoints_folder() =>
        FileOrganizationRules.EndpointClassesShouldResideInEndpointsFolder(SrcDir);

    [Fact]
    public void Dto_classes_should_reside_in_Dtos_folder() =>
        FileOrganizationRules.DtoClassesShouldResideInDtosFolder(SrcDir);

    [Fact]
    public void EfCore_configurations_should_reside_in_Configurations_folder() =>
        FileOrganizationRules.EfCoreConfigurationsShouldResideInConfigurationsFolder(SrcDir);

    [Fact]
    public void Endpoint_validators_should_reside_in_Validators_folder() =>
        FileOrganizationRules.EndpointValidatorsShouldResideInValidatorsFolder(SrcDir);

    [Fact]
    public void Permission_definitions_should_reside_in_Permissions_folder() =>
        FileOrganizationRules.PermissionDefinitionsShouldResideInPermissionsFolder(SrcDir);

    [Fact]
    public void Metrics_classes_should_reside_in_Diagnostics_folder() =>
        FileOrganizationRules.MetricsClassesShouldResideInDiagnosticsFolder(SrcDir);

    [Fact]
    public void ActivitySource_classes_should_reside_in_Diagnostics_folder() =>
        FileOrganizationRules.ActivitySourceClassesShouldResideInDiagnosticsFolder(SrcDir);

    [Fact]
    public void DbContext_classes_should_reside_in_Internal_folder() =>
        FileOrganizationRules.DbContextClassesShouldResideInInternalFolder(SrcDir);

    [Fact]
    public void Domain_types_should_reside_in_correct_folder() =>
        FileOrganizationRules.DomainTypesShouldResideInCorrectFolder(SrcDir,
            // Granit defines the base classes themselves
            "Granit");
}
