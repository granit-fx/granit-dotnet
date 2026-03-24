using Bogus;
using Granit.Domain;
using Granit.Testing.Generators;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitEntityFakerExtensionsTests
{
    [Fact]
    public void RuleForCreationAudit_Populates_Fields()
    {
        TestCreationAuditedEntity entity = new Faker<TestCreationAuditedEntity>()
            .RuleForCreationAudit()
            .Generate();

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.CreatedAt.ShouldNotBe(default);
        entity.CreatedBy.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RuleForAudit_Populates_All_Audit_Fields()
    {
        List<TestAuditedEntity> entities = new Faker<TestAuditedEntity>()
            .RuleForAudit()
            .Generate(20);

        entities.ShouldAllBe(e => e.Id != Guid.Empty);
        entities.ShouldAllBe(e => e.CreatedAt != default);
        entities.ShouldAllBe(e => !string.IsNullOrWhiteSpace(e.CreatedBy));
    }

    [Fact]
    public void RuleForFullAudit_Sets_NonDeleted_Defaults()
    {
        TestFullAuditedEntity entity = new Faker<TestFullAuditedEntity>()
            .RuleForFullAudit()
            .Generate();

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.IsDeleted.ShouldBeFalse();
        entity.DeletedAt.ShouldBeNull();
        entity.DeletedBy.ShouldBeNull();
    }

    [Fact]
    public void RuleForMultiTenant_Uses_Fixed_TenantId_When_Provided()
    {
        var tenantId = Guid.NewGuid();

        TestMultiTenantEntity entity = new Faker<TestMultiTenantEntity>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleForMultiTenant(tenantId)
            .Generate();

        entity.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void RuleForMultiTenant_Generates_Random_TenantId_When_Null()
    {
        List<TestMultiTenantEntity> entities = new Faker<TestMultiTenantEntity>()
            .RuleFor(e => e.Id, f => f.Random.Guid())
            .RuleForMultiTenant()
            .Generate(5);

        entities.Select(e => e.TenantId).Distinct().Count().ShouldBeGreaterThan(1);
    }

    // ---- Test entity classes ----

    private sealed class TestCreationAuditedEntity : CreationAuditedEntity;

    private sealed class TestAuditedEntity : AuditedEntity;

    private sealed class TestFullAuditedEntity : FullAuditedEntity;

    private sealed class TestMultiTenantEntity : Entity, IMultiTenant
    {
        public Guid? TenantId { get; set; }
    }
}
