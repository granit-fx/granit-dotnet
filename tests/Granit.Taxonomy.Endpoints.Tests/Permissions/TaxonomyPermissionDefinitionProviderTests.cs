using Granit.Taxonomy.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Permissions;

public sealed class TaxonomyPermissionDefinitionProviderTests
{
    [Fact]
    public void Permission_Constants_FollowGranitFormat()
    {
        TaxonomyPermissions.Tags.Read.ShouldBe("Taxonomy.Tags.Read");
        TaxonomyPermissions.Tags.Manage.ShouldBe("Taxonomy.Tags.Manage");
        TaxonomyPermissions.Categories.Read.ShouldBe("Taxonomy.Categories.Read");
        TaxonomyPermissions.Categories.Manage.ShouldBe("Taxonomy.Categories.Manage");
        TaxonomyPermissions.GroupName.ShouldBe("Taxonomy");
    }

    [Fact]
    public void DefinePermissions_NullContext_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            new TaxonomyPermissionDefinitionProvider().DefinePermissions(null!));
}
