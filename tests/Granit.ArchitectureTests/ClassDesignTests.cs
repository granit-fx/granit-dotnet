using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates class design conventions: sealed DbContexts, internal Ef*Store implementations,
/// no MVC controllers, sealed Options classes, EF configuration confinement.
/// </summary>
public sealed class ClassDesignTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void DbContext_classes_should_be_sealed() =>
        ClassDesignRules.DbContextClassesShouldBeSealed(Architecture);

    [Fact]
    public void EfStore_implementations_should_not_be_public() =>
        ClassDesignRules.EfStoreImplementationsShouldNotBePublic(Architecture);

    [Fact]
    public void No_MVC_controllers_allowed() =>
        Should.NotThrow(() => ClassDesignRules.NoMvcControllersAllowed(Architecture, "Granit."));

    [Fact]
    public void Options_classes_should_be_sealed() =>
        ClassDesignRules.OptionsClassesShouldBeSealed(Architecture, "Granit.");

    [Fact]
    public void EntityTypeConfigurations_should_be_in_EfCore_layer() =>
        ClassDesignRules.EntityTypeConfigurationsShouldBeInEfCoreLayer(Architecture, "Granit.",
            // Granit.BlobStorage.Database is a persistence layer that stores blobs in the database.
            "Database");

    [Fact]
    public void Entity_configurations_should_not_be_public() =>
        ClassDesignRules.EntityConfigurationsShouldNotBePublic(Architecture, "Granit.");

    [Fact]
    public void Public_types_should_not_reside_in_Internal_namespaces() =>
        ClassDesignRules.PublicTypesShouldNotResideInInternalNamespaces(
            Architecture, "Granit.",
            // Wolverine requires middleware constructor parameters to be public,
            // even when the interface is an internal implementation detail.
            "Granit.Wolverine.Internal.IWolverineUserContextSetter");

    [Fact]
    public void Concrete_exception_classes_should_be_sealed() =>
        ClassDesignRules.ConcreteExceptionClassesShouldBeSealed(Architecture, "Granit.");
}
