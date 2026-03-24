using Bogus;
using Granit.Domain;
using Granit.Testing.Generators;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitEntityFakerExtensionsAdditionalTests
{
    [Fact]
    public void RuleForCreationAudit_ThrowsOnNull()
    {
        Faker<TestCreationEntity>? faker = null;

        Should.Throw<ArgumentNullException>(() => faker!.RuleForCreationAudit());
    }

    [Fact]
    public void RuleForAudit_ThrowsOnNull()
    {
        Faker<TestAuditEntity>? faker = null;

        Should.Throw<ArgumentNullException>(() => faker!.RuleForAudit());
    }

    [Fact]
    public void RuleForFullAudit_ThrowsOnNull()
    {
        Faker<TestFullEntity>? faker = null;

        Should.Throw<ArgumentNullException>(() => faker!.RuleForFullAudit());
    }

    [Fact]
    public void RuleForMultiTenant_ThrowsOnNull()
    {
        Faker<TestTenantEntity>? faker = null;

        Should.Throw<ArgumentNullException>(() => faker!.RuleForMultiTenant());
    }

    [Fact]
    public void RuleForCreationAudit_Generates_Unique_Ids()
    {
        List<TestCreationEntity> entities = new Faker<TestCreationEntity>()
            .RuleForCreationAudit()
            .Generate(10);

        entities.Select(e => e.Id).Distinct().Count().ShouldBe(10);
    }

    [Fact]
    public void RuleForAudit_Includes_Creation_Audit_Fields()
    {
        TestAuditEntity entity = new Faker<TestAuditEntity>()
            .RuleForAudit()
            .Generate();

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.CreatedAt.ShouldNotBe(default);
        entity.CreatedBy.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RuleForFullAudit_Includes_Audit_Fields()
    {
        TestFullEntity entity = new Faker<TestFullEntity>()
            .RuleForFullAudit()
            .Generate();

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.CreatedAt.ShouldNotBe(default);
        entity.CreatedBy.ShouldNotBeNullOrWhiteSpace();
        entity.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void RuleForMultiTenant_With_Fixed_Id_All_Same()
    {
        var tenantId = Guid.NewGuid();

        List<TestTenantEntity> entities = new Faker<TestTenantEntity>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleForMultiTenant(tenantId)
            .Generate(5);

        entities.ShouldAllBe(e => e.TenantId == tenantId);
    }

    // ---- Test entity classes ----

    private sealed class TestCreationEntity : CreationAuditedEntity;

    private sealed class TestAuditEntity : AuditedEntity;

    private sealed class TestFullEntity : FullAuditedEntity;

    private sealed class TestTenantEntity : Entity, IMultiTenant
    {
        public Guid? TenantId { get; set; }
    }
}
