using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Minimal host DbContext used by Postgres integration tests — wires the
/// <see cref="ManagedHostname"/> entity configuration via
/// <see cref="HostnamesModelBuilderExtensions.ConfigureHostnamesModule"/> and applies Granit
/// conventions (SVO value converters, enum-as-string) via <c>ApplyGranitConventions()</c>.
/// </summary>
internal sealed class TestHostnamesDbContext(DbContextOptions<TestHostnamesDbContext> options)
    : DbContext(options)
{
    public DbSet<ManagedHostname> ManagedHostnames => Set<ManagedHostname>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyGranitConventions();
        modelBuilder.ConfigureHostnamesModule();
    }
}
