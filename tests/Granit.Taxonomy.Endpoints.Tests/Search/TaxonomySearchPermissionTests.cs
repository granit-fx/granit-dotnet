using Granit.Taxonomy.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Search;

public sealed class TaxonomySearchPermissionTests
{
    [Fact]
    public void SearchPermissionConstant_FollowsGranitFormat() =>
        TaxonomyPermissions.Search.Read.ShouldBe("Taxonomy.Search.Read");
}
