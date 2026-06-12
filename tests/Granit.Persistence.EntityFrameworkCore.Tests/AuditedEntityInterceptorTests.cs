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
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

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
    // AuditedAggregateRoot (création + modification)
    // -------------------------------------------------------------------------
    // Régression : la hiérarchie AggregateRoot est disjointe de la hiérarchie
    // Entity. ModifiedAt/ModifiedBy étaient laissés null sur les agrégats parce
    // que l'intercepteur castait vers AuditedEntity (qu'un agrégat n'est jamais).
    // Le pivot sur IModificationAuditedObject couvre désormais les deux.

    [Fact]
    public async Task SaveChangesAsync_AuditedAggregateRoot_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedAggregateRoot aggregate = new() { Name = "Aggregate" };
        context.AuditedAggregateRoots.Add(aggregate);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        aggregate.CreatedAt.ShouldBe(FixedNow);
        aggregate.CreatedBy.ShouldBe("user-test-123");
        aggregate.Id.ShouldBe(FixedGuid);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditedAggregateRoot_OnModify_SetsModifiedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedAggregateRoot aggregate = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "original-user"
        };
        context.AuditedAggregateRoots.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modifier l'agrégat
        aggregate.Name = "Modified";
        context.Entry(aggregate).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — c'est le cœur de la régression corrigée
        aggregate.ModifiedAt.ShouldBe(FixedNow);
        aggregate.ModifiedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_AuditedAggregateRoot_OnModify_DoesNotOverwriteCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestAuditedAggregateRoot aggregate = new()
        {
            Id = Guid.NewGuid(),
            Name = "Original"
        };
        context.AuditedAggregateRoots.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        DateTimeOffset originalCreatedAt = aggregate.CreatedAt;
        string originalCreatedBy = aggregate.CreatedBy;

        // Avancer le temps pour le Modify
        _clock.Now.Returns(FixedNow.AddHours(1));

        // Modifier l'agrégat
        aggregate.Name = "Modified";
        context.Entry(aggregate).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — les champs de création restent protégés sur un agrégat aussi
        aggregate.CreatedAt.ShouldBe(originalCreatedAt);
        aggregate.CreatedBy.ShouldBe(originalCreatedBy);
        aggregate.ModifiedAt.ShouldBe(FixedNow.AddHours(1));
    }

    // -------------------------------------------------------------------------
    // FullAuditedAggregateRoot (création + modification + soft delete)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_FullAuditedAggregateRoot_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestFullAuditedAggregateRoot aggregate = new() { Title = "Full" };
        context.FullAuditedAggregateRoots.Add(aggregate);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        aggregate.CreatedAt.ShouldBe(FixedNow);
        aggregate.CreatedBy.ShouldBe("user-test-123");
        aggregate.Id.ShouldBe(FixedGuid);
        aggregate.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_FullAuditedAggregateRoot_OnModify_SetsModifiedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestFullAuditedAggregateRoot aggregate = new()
        {
            Id = Guid.NewGuid(),
            Title = "Original"
        };
        context.FullAuditedAggregateRoots.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modifier l'agrégat
        aggregate.Title = "Updated";
        context.Entry(aggregate).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        aggregate.ModifiedAt.ShouldBe(FixedNow);
        aggregate.ModifiedBy.ShouldBe("user-test-123");
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
    // ICreationAuditedObject sur une entité hors hiérarchie Entity
    // -------------------------------------------------------------------------
    // Régression : LocalIdentity étend IdentityUser<Guid> et ne PEUT donc pas
    // dériver de CreationAuditedEntity. Avant le pivot sur ICreationAuditedObject
    // l'intercepteur l'ignorait, CreatedAt restait à default(DateTimeOffset) et
    // Npgsql le persistait en -infinity. Le pivot interface couvre désormais ce cas.

    [Fact]
    public async Task SaveChangesAsync_IdentityLikeEntity_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestIdentityLikeAuditable entity = new() { Id = Guid.NewGuid(), Name = "Alice" };
        context.IdentityLikeAuditables.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — plus de default(DateTimeOffset) → plus de -infinity en base
        entity.CreatedAt.ShouldBe(FixedNow);
        entity.CreatedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_IdentityLikeEntity_OnModify_SetsModifiedFields_AndProtectsCreated()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        TestIdentityLikeAuditable entity = new() { Id = Guid.NewGuid(), Name = "Alice" };
        context.IdentityLikeAuditables.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        DateTimeOffset originalCreatedAt = entity.CreatedAt;
        string originalCreatedBy = entity.CreatedBy;
        _clock.Now.Returns(FixedNow.AddHours(1));

        // Act
        entity.Name = "Alice Updated";
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.ModifiedAt.ShouldBe(FixedNow.AddHours(1));
        entity.ModifiedBy.ShouldBe("user-test-123");
        entity.CreatedAt.ShouldBe(originalCreatedAt);
        entity.CreatedBy.ShouldBe(originalCreatedBy);
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

    private sealed class TestAuditedAggregateRoot : AuditedAggregateRoot
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestFullAuditedAggregateRoot : FullAuditedAggregateRoot
    {
        public string Title { get; set; } = string.Empty;
    }

    private sealed class TestMultiTenantEntity : CreationAuditedEntity, IMultiTenant
    {
        public string Name { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
    }

    // Mimics LocalIdentity: owns a Guid PK but does NOT derive from Entity /
    // CreationAuditedEntity — it only implements the audit interfaces.
    private sealed class TestIdentityLikeAuditable : ICreationAuditedObject, IModificationAuditedObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<AuditedEntityInterceptorTests.TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestCreationAuditedEntity> CreationAuditedEntities => Set<TestCreationAuditedEntity>();
        public DbSet<TestAuditedEntity> AuditedEntities => Set<TestAuditedEntity>();
        public DbSet<TestFullAuditedEntity> FullAuditedEntities => Set<TestFullAuditedEntity>();
        public DbSet<TestAuditedAggregateRoot> AuditedAggregateRoots => Set<TestAuditedAggregateRoot>();
        public DbSet<TestFullAuditedAggregateRoot> FullAuditedAggregateRoots => Set<TestFullAuditedAggregateRoot>();
        public DbSet<TestMultiTenantEntity> MultiTenantEntities => Set<TestMultiTenantEntity>();
        public DbSet<TestIdentityLikeAuditable> IdentityLikeAuditables => Set<TestIdentityLikeAuditable>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ValueGeneratedNever : l'intercepteur gère la génération des GUID
            modelBuilder.Entity<TestCreationAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestFullAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestMultiTenantEntity>().Property(e => e.Id).ValueGeneratedNever();

            // Hors hiérarchie Entity : PK déclarée explicitement, posée par l'appelant.
            modelBuilder.Entity<TestIdentityLikeAuditable>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            // Aggregate roots expose event collections that are not persisted columns.
            modelBuilder.Entity<TestAuditedAggregateRoot>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Ignore(e => e.DomainEvents);
                b.Ignore(e => e.IntegrationEvents);
            });
            modelBuilder.Entity<TestFullAuditedAggregateRoot>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Ignore(e => e.DomainEvents);
                b.Ignore(e => e.IntegrationEvents);
            });
        }
    }
}
