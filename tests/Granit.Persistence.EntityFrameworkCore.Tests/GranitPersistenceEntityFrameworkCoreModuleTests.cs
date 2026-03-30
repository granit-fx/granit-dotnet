// =============================================================================
// Tests - GranitPersistenceEntityFrameworkCoreModule
// =============================================================================
// Vérifie que le module :
//   - Enregistre les intercepteurs EF Core via ConfigureServices
//   - Déclare les dépendances [DependsOn] correctes
// =============================================================================

using Granit.Guids;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class GranitPersistenceEntityFrameworkCoreModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersInterceptors()
    {
        // Arrange
        GranitPersistenceEntityFrameworkCoreModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? auditDescriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(AuditedEntityInterceptor));
        auditDescriptor.ShouldNotBeNull();

        ServiceDescriptor? softDeleteDescriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(SoftDeleteInterceptor));
        softDeleteDescriptor.ShouldNotBeNull();
    }

    [Fact]
    public void DependsOn_DeclaresCorrectDependencies()
    {
        // Arrange
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        // Assert
        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitTimingModule));
        dependedTypes.ShouldContain(typeof(GranitGuidsModule));
        dependedTypes.ShouldNotContain(typeof(GranitMultiTenancyModule),
            "ICurrentTenant is now sourced from Granit.MultiTenancy — Granit.MultiTenancy is a soft dependency");
    }
}
