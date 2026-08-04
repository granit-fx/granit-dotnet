using Granit.Testing.Persistence.Suites;
using Xunit;

namespace Granit.Testing.Persistence.Tests.Integration;

[Collection(PostgresConformanceSuite.Name)]
public sealed class PostgresInterceptorPipelineTests(PostgresConformanceFixture fixture)
    : InterceptorPipelineConformanceSuite(fixture);
