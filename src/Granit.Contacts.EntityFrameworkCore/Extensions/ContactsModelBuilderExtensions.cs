using Granit.Contacts.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore.Extensions;

/// <summary>ModelBuilder extensions for Granit.Contacts entity configurations.</summary>
public static class ContactsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Contacts module.</summary>
    public static ModelBuilder ConfigureContactsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ContactConfiguration());
        modelBuilder.ApplyConfiguration(new ContactAddressConfiguration());
        modelBuilder.ApplyConfiguration(new ContactEmailConfiguration());
        modelBuilder.ApplyConfiguration(new ContactPhoneConfiguration());
        modelBuilder.ApplyConfiguration(new ContactExternalMappingConfiguration());
        return modelBuilder;
    }
}
