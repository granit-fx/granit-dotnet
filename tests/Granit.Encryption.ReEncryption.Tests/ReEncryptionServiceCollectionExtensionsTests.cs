using Granit.Encryption.ReEncryption;
using Granit.Encryption.ReEncryption.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Encryption.ReEncryption.Tests;

public sealed class ReEncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitEncryptionReEncryption_Registers_ReEncryptionJob()
    {
        ServiceCollection services = new();
        services.AddGranitEncryptionReEncryption<FakeDbContext>();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IReEncryptionJob));

        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(DefaultReEncryptionJob<FakeDbContext>));
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
