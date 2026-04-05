using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class MultiTenancyModelBuilderExtensionsTests
{
    [Fact]
    public void ConfigureMultiTenancyModule_RegistersTenantEntity()
    {
        DbContextOptionsBuilder<DbContext> optionsBuilder = new();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());

        ModelBuilder modelBuilder = new();
        modelBuilder.ConfigureMultiTenancyModule();

        IModel model = modelBuilder.FinalizeModel();
        IEntityType? entityType = model.FindEntityType(typeof(Tenant));

        entityType.ShouldNotBeNull();
    }
}
