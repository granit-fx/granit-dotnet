// =============================================================================
// Tests - PermissionDefinitionManager
// =============================================================================
// Vérifie que le manager :
//   - Agrège correctement les permissions de plusieurs providers
//   - Applique le pattern GetOrAdd sur les groupes (multi-modules)
//   - Détecte les permissions existantes et inconnues
// =============================================================================

using Granit.Authorization.Abstractions;
using Granit.Authorization.Services;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionDefinitionManagerTests
{
    [Fact]
    public void Constructor_SingleProvider_RegistersAllPermissions()
    {
        // Arrange
        IPermissionDefinitionProvider[] providers = [new InvoicesPermissionProvider()];

        // Act
        PermissionDefinitionManager manager = new(providers);

        // Assert
        manager.Exists("Invoices.Read").ShouldBeTrue();
        manager.Exists("Invoices.Create").ShouldBeTrue();
        manager.Exists("Invoices.Delete").ShouldBeTrue();
    }

    [Fact]
    public void Constructor_TwoProvidersAddingToSameGroup_BothPermissionsPresent()
    {
        // Arrange — deux modules partagent le groupe "Administration"
        IPermissionDefinitionProvider[] providers =
        [
            new AdminPermissionProvider1(),
            new AdminPermissionProvider2()
        ];

        // Act — aucune exception (pattern GetOrAdd sur les groupes)
        PermissionDefinitionManager manager = new(providers);

        // Assert — les permissions des deux providers sont disponibles
        manager.Exists("Administration.Users.Read").ShouldBeTrue();
        manager.Exists("Administration.Reports.Export").ShouldBeTrue();
    }

    [Fact]
    public void Exists_UnknownPermission_ReturnsFalse()
    {
        // Arrange
        PermissionDefinitionManager manager = new([new InvoicesPermissionProvider()]);

        // Act & Assert
        manager.Exists("Unknown.Permission").ShouldBeFalse();
    }

    [Fact]
    public void GetAll_ReturnsAllPermissionsAcrossAllGroups()
    {
        // Arrange
        IPermissionDefinitionProvider[] providers =
        [
            new InvoicesPermissionProvider(),
            new AdminPermissionProvider1()
        ];

        // Act
        PermissionDefinitionManager manager = new(providers);
        IReadOnlyList<PermissionDefinition> all = manager.GetAll();

        // Assert
        all.ShouldContain(p => p.Name == "Invoices.Read");
        all.ShouldContain(p => p.Name == "Invoices.Delete");
        all.ShouldContain(p => p.Name == "Administration.Users.Read");
    }

    [Fact]
    public void GetGroups_ReturnsAllDeclaredGroups()
    {
        // Arrange
        IPermissionDefinitionProvider[] providers = [new InvoicesPermissionProvider()];
        PermissionDefinitionManager manager = new(providers);

        // Act
        IReadOnlyList<PermissionGroup> groups = manager.GetGroups();

        // Assert
        groups.ShouldContain(g => g.Name == "Invoices");
    }

    [Fact]
    public void Constructor_TwoProvidersAddingToSameGroup_GroupAppearsOnce()
    {
        // Arrange — deux providers enrichissent le même groupe
        IPermissionDefinitionProvider[] providers =
        [
            new AdminPermissionProvider1(),
            new AdminPermissionProvider2()
        ];

        // Act
        PermissionDefinitionManager manager = new(providers);

        // Assert — le groupe "Administration" n'est présent qu'une fois
        manager.GetGroups().Where(g => g.Name == "Administration").Count().ShouldBe(1);
    }

    // --- Test doubles ---

    private sealed class InvoicesPermissionProvider : IPermissionDefinitionProvider
    {
        public void DefinePermissions(IPermissionDefinitionContext context)
        {
            PermissionGroup group = context.AddGroup("Invoices", LocalizableString.Fixed("Factures"));
            group.AddPermission("Invoices.Read");
            group.AddPermission("Invoices.Create");
            group.AddPermission("Invoices.Delete");
        }
    }

    private sealed class AdminPermissionProvider1 : IPermissionDefinitionProvider
    {
        public void DefinePermissions(IPermissionDefinitionContext context)
        {
            PermissionGroup group = context.AddGroup("Administration");
            group.AddPermission("Administration.Users.Read");
        }
    }

    private sealed class AdminPermissionProvider2 : IPermissionDefinitionProvider
    {
        public void DefinePermissions(IPermissionDefinitionContext context)
        {
            PermissionGroup group = context.AddGroup("Administration"); // même groupe — GetOrAdd
            group.AddPermission("Administration.Reports.Export");
        }
    }
}
