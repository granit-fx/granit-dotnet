using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Extensions;
using Granit.Http.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadHttpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitHttpBulkhead_RegistersExceptionStatusCodeMapper()
    {
        ServiceCollection services = new();

        services.AddGranitHttpBulkhead();

        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IExceptionStatusCodeMapper> mappers = provider.GetServices<IExceptionStatusCodeMapper>();
        mappers.ShouldContain(m => m is BulkheadExceptionStatusCodeMapper);
    }
}
