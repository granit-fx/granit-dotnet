using Granit.Encryption.EntityFrameworkCore;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class ReEncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitEncryptionReEncryption_Registers_ReEncryptionService()
    {
        ServiceCollection services = new();
        services.AddGranitEncryptionReEncryption<FakeDbContext>();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IReEncryptionService));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(DefaultReEncryptionService<FakeDbContext>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitEncryptionReEncryption_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitEncryptionReEncryption<FakeDbContext>();

        result.ShouldBeSameAs(services);
    }

    private sealed class FakeDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseInMemoryDatabase("fake");
    }
}
