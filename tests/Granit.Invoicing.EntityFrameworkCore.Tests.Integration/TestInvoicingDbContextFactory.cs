using Granit.Invoicing.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore.Tests.Integration;

internal sealed class TestInvoicingDbContextFactory : IDbContextFactory<InvoicingDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<InvoicingDbContext> _options;

    public TestInvoicingDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public InvoicingDbContext CreateDbContext() => new(_options);

    public Task<InvoicingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new InvoicingDbContext(_options));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
