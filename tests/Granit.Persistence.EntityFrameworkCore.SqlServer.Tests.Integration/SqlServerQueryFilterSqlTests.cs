using Granit.Testing.Persistence.Suites;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests.Integration;

[Collection(SqlServerConformanceSuite.Name)]
public sealed class SqlServerQueryFilterSqlTests(SqlServerConformanceFixture fixture)
    : QueryFilterSqlConformanceSuite(fixture);
