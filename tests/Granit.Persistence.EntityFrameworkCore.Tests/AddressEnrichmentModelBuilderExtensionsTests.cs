using Granit.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class AddressEnrichmentModelBuilderExtensionsTests
{
    [Fact]
    public void MapAddressGeocoding_MapsFlatColumns_IgnoresComputedCoordinate_AndIsNotAnEntityType()
    {
        using EnrichmentDbContext context = NewContext();

        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes().Select(et => et.ClrType.Name)];
        entityNames.ShouldNotContain(nameof(AddressGeocoding), "AddressGeocoding must be a complex type, not an entity");

        IComplexProperty geocoding = context.Model
            .FindEntityType(typeof(AddressHolder))!
            .FindComplexProperty(nameof(AddressHolder.Geocoding))
            .ShouldNotBeNull("Geocoding must be mapped as a complex property");

        IReadOnlyList<string> columns = [.. geocoding.ComplexType.GetProperties().Select(p => p.Name)];
        columns.ShouldNotContain(nameof(AddressGeocoding.Coordinate), "the computed coordinate must be ignored");
        columns.ShouldContain(nameof(AddressGeocoding.Latitude));
        columns.ShouldContain(nameof(AddressGeocoding.Longitude));
        columns.ShouldContain(nameof(AddressGeocoding.Status));
    }

    [Fact]
    public void MapAddressGeocoding_PersistsEnumsAsString()
    {
        using EnrichmentDbContext context = NewContext();
        IComplexProperty geocoding = context.Model
            .FindEntityType(typeof(AddressHolder))!
            .FindComplexProperty(nameof(AddressHolder.Geocoding))!;

        ProviderType(geocoding, nameof(AddressGeocoding.Status)).ShouldBe(typeof(string));
        ProviderType(geocoding, nameof(AddressGeocoding.MatchPrecision)).ShouldBe(typeof(string));
    }

    [Fact]
    public void MapAddressVerification_PersistsEnumsAsString()
    {
        using EnrichmentDbContext context = NewContext();
        IComplexProperty verification = context.Model
            .FindEntityType(typeof(AddressHolder))!
            .FindComplexProperty(nameof(AddressHolder.Verification))!;

        ProviderType(verification, nameof(AddressVerification.Status)).ShouldBe(typeof(string));
        ProviderType(verification, nameof(AddressVerification.Source)).ShouldBe(typeof(string));
    }

    private static Type? ProviderType(IComplexProperty complex, string propertyName)
    {
        IProperty property = complex.ComplexType.FindProperty(propertyName)!;
        return property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType();
    }

    private static EnrichmentDbContext NewContext() =>
        new(new DbContextOptionsBuilder<EnrichmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public sealed class AddressHolder
    {
        public Guid Id { get; set; }
        public AddressGeocoding Geocoding { get; set; } = AddressGeocoding.Pending;
        public AddressVerification Verification { get; set; } = AddressVerification.Unverified;
    }

    private sealed class EnrichmentDbContext(DbContextOptions<EnrichmentDbContext> options) : DbContext(options)
    {
        public DbSet<AddressHolder> Holders => Set<AddressHolder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AddressHolder>(builder =>
            {
                builder.MapAddressGeocoding(e => e.Geocoding);
                builder.MapAddressVerification(e => e.Verification);
            });
            modelBuilder.ApplyGranitConventions();
        }
    }
}
