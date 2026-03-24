// =============================================================================
// Tests - AuditedEntityInterceptor
// =============================================================================
// Vérifie que les champs d'audit ISO 27001 sont correctement remplis
// lors de la création et modification des entités, pour chaque niveau
// de la hiérarchie (CreationAuditedEntity, AuditedEntity, FullAuditedEntity).
// Vérifie également l'injection automatique du TenantId sur les entités
// implémentant IMultiTenant.
//
// Approche : on enregistre l'intercepteur dans le DbContext et on appelle
// SaveChangesAsync directement, ce qui déclenche l'intercepteur naturellement.
// IClock est mocké pour des assertions exactes (pas de BeCloseTo).
// =============================================================================

using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class AuditedEntityInterceptorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid FixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    private static readonly Guid TenantId = Guid.Parse("aaaabbbb-0000-0000-0000-000000000001");

    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public AuditedEntityInterceptorTests()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("user-test-123");

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(FixedNow);

        _guidGenerator = Substitute.For<IGuidGenerator>();
        _guidGenerator.Create().Returns(FixedGuid);

        _currentTenant = Substitute.For<ICurrentTenant>();
        _currentTenant.Id.Returns((Guid?)null);
    }

    // -------------------------------------------------------------------------
    // AuditedEntity (création + modification)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedEntity entity = new() { Name = "Test" };
        context.AuditedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedAt.ShouldBe(FixedNow);
        entity.CreatedBy.ShouldBe("user-test-123");
        entity.Id.ShouldBe(FixedGuid);
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_SetsModifiedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "original-user"
        };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modifier l'entité
        entity.Name = "Modified";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.ModifiedAt.ShouldBe(FixedNow);
        entity.ModifiedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_DoesNotOverwriteCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original"
        };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Capturer les valeurs de création posées par l'intercepteur lors du Add
        DateTimeOffset originalCreatedAt = entity.CreatedAt;
        string? originalCreatedBy = entity.CreatedBy;

        // Avancer le temps pour le Modify
        _clock.Now.Returns(FixedNow.AddHours(1));

        // Modifier l'entité
        entity.Name = "Modified";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — les champs de création ne doivent pas être modifiés
        entity.CreatedAt.ShouldBe(originalCreatedAt);
        entity.CreatedBy.ShouldBe(originalCreatedBy);
        // Mais ModifiedAt doit refléter le nouveau temps
        entity.ModifiedAt.ShouldBe(FixedNow.AddHours(1));
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutUser_UsesSystem()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        await using TestDbContext context = CreateContext();
        TestAuditedEntity entity = new() { Name = "Test" };
        context.AuditedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedBy.ShouldBe("system");
    }

    // -------------------------------------------------------------------------
    // CreationAuditedEntity (création uniquement, pas de ModifiedAt/By)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_CreationAuditedEntity_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestCreationAuditedEntity entity = new() { Label = "Immutable" };
        context.CreationAuditedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedAt.ShouldBe(FixedNow);
        entity.CreatedBy.ShouldBe("user-test-123");
        entity.Id.ShouldBe(FixedGuid);
    }

    [Fact]
    public async Task SaveChangesAsync_CreationAuditedEntity_OnModify_ProtectsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestCreationAuditedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Label = "Original"
        };
        context.CreationAuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        DateTimeOffset originalCreatedAt = entity.CreatedAt;
        string originalCreatedBy = entity.CreatedBy;

        // Avancer le temps
        _clock.Now.Returns(FixedNow.AddHours(1));

        // Modifier l'entité
        entity.Label = "Modified";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — les champs de création sont protégés
        entity.CreatedAt.ShouldBe(originalCreatedAt);
        entity.CreatedBy.ShouldBe(originalCreatedBy);
    }

    // -------------------------------------------------------------------------
    // FullAuditedEntity (création + modification + soft delete via ISoftDeletable)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_FullAuditedEntity_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestFullAuditedEntity entity = new() { Title = "Full" };
        context.FullAuditedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedAt.ShouldBe(FixedNow);
        entity.CreatedBy.ShouldBe("user-test-123");
        entity.Id.ShouldBe(FixedGuid);
        entity.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_FullAuditedEntity_OnModify_SetsModifiedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestFullAuditedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Title = "Original"
        };
        context.FullAuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modifier l'entité
        entity.Title = "Updated";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.ModifiedAt.ShouldBe(FixedNow);
        entity.ModifiedBy.ShouldBe("user-test-123");
    }

    // -------------------------------------------------------------------------
    // IMultiTenant — injection automatique du TenantId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_MultiTenant_OnAdd_SetsTenantId_WhenTenantIsActive()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns((Guid?)TenantId);
        await using TestDbContext context = CreateContext();
        TestMultiTenantEntity entity = new() { Name = "Fiche patient" };
        context.MultiTenantEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_MultiTenant_OnAdd_TenantIdIsNull_WhenNoTenantActive()
    {
        // Arrange — aucun tenant actif (contexte système)
        _currentTenant.Id.Returns((Guid?)null);
        await using TestDbContext context = CreateContext();
        TestMultiTenantEntity entity = new() { Name = "Donnée globale" };
        context.MultiTenantEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_MultiTenant_OnAdd_DoesNotOverwriteExistingTenantId()
    {
        // Arrange — TenantId déjà défini explicitement (migration, import)
        var explicitTenant = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns((Guid?)TenantId);
        await using TestDbContext context = CreateContext();
        TestMultiTenantEntity entity = new() { Name = "Import", TenantId = explicitTenant };
        context.MultiTenantEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — le TenantId explicite est conservé
        entity.TenantId.ShouldBe(explicitTenant);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private TestDbContext CreateContext()
    {
        AuditedEntityInterceptor interceptor = new(_currentUserService, _clock, _guidGenerator, _currentTenant);
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestCreationAuditedEntity : CreationAuditedEntity
    {
        public string Label { get; set; } = string.Empty;
    }

    private sealed class TestAuditedEntity : AuditedEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestFullAuditedEntity : FullAuditedEntity
    {
        public string Title { get; set; } = string.Empty;
    }

    private sealed class TestMultiTenantEntity : CreationAuditedEntity, IMultiTenant
    {
        public string Name { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<AuditedEntityInterceptorTests.TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestCreationAuditedEntity> CreationAuditedEntities => Set<TestCreationAuditedEntity>();
        public DbSet<TestAuditedEntity> AuditedEntities => Set<TestAuditedEntity>();
        public DbSet<TestFullAuditedEntity> FullAuditedEntities => Set<TestFullAuditedEntity>();
        public DbSet<TestMultiTenantEntity> MultiTenantEntities => Set<TestMultiTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ValueGeneratedNever : l'intercepteur gère la génération des GUID
            modelBuilder.Entity<TestCreationAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestFullAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestMultiTenantEntity>().Property(e => e.Id).ValueGeneratedNever();
        }
    }
}
