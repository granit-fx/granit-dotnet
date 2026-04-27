using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Parties entity configurations.</summary>
public static class PartiesModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Parties module.</summary>
    public static ModelBuilder ConfigureContactsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PartyConfiguration());
        modelBuilder.ApplyConfiguration(new PartyAddressConfiguration());
        modelBuilder.ApplyConfiguration(new PartyEmailConfiguration());
        modelBuilder.ApplyConfiguration(new PartyPhoneConfiguration());
        modelBuilder.ApplyConfiguration(new PartyExternalMappingConfiguration());
        return modelBuilder;
    }
}
