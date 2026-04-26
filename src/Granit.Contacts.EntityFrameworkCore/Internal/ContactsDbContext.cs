using Granit.Contacts.Domain;
using Granit.Contacts.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore.Internal;

/// <summary>Dedicated EF Core <see cref="DbContext"/> for the central <see cref="Contact"/> aggregate.</summary>
internal sealed class ContactsDbContext(
    DbContextOptions<ContactsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Contact> Contacts { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureContactsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
