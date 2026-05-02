using Granit.Documents.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests;

public sealed class DocumentsDbContextTests
{
    [Fact]
    public void OnModelCreating_RunsWithoutError_OnEmptyModel()
    {
        // Phase-1 scaffolding: the DbContext has no DbSets yet. Verify the wiring
        // (ApplyConfigurations + ApplyGranitConventions) does not blow up on an empty model.
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(OnModelCreating_RunsWithoutError_OnEmptyModel))
            .Options;

        using DocumentsDbContext context = new(options);

        // Forcing model creation via Database.EnsureCreated would require a relational provider;
        // accessing Model triggers OnModelCreating against the in-memory provider.
        Microsoft.EntityFrameworkCore.Metadata.IModel model = context.Model;

        model.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_AcceptsNullCurrentTenantAndDataFilter()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Constructor_AcceptsNullCurrentTenantAndDataFilter))
            .Options;

        // The optional ICurrentTenant? / IDataFilter? parameters allow the DbContext to be
        // constructed without the multi-tenancy / data-filter modules registered.
        using DocumentsDbContext context = new(options, currentTenant: null, dataFilter: null);

        context.Model.ShouldNotBeNull();
    }
}
