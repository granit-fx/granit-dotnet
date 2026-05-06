using Granit.Domain;
using Granit.Events;
using Granit.Taxonomy.Internal;
using Granit.Taxonomy.Registration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Internal;

public sealed class TaxonomyAssignmentCleanupHandlerTests
{
    private sealed class FakeDocument : Entity, IEmitEntityLifecycleEvents;
    private sealed class FakeUnregistered : Entity, IEmitEntityLifecycleEvents;

    private static readonly string DocumentTargetType = typeof(FakeDocument).FullName!;

    private readonly ITagAssignmentService _tagAssignments = Substitute.For<ITagAssignmentService>();
    private readonly ICategoryAssignmentService _categoryAssignments = Substitute.For<ICategoryAssignmentService>();

    [Fact]
    public async Task HandleAsync_RegisteredType_RemovesBothTagAndCategoryAssignments()
    {
        TaggableTypeRegistry registry = new();
        registry.Register(DocumentTargetType, "documents");

        _tagAssignments
            .RemoveAllAssignmentsAsync(DocumentTargetType, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(3);
        _categoryAssignments
            .RemoveAllAssignmentsAsync(DocumentTargetType, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(1);

        TaxonomyAssignmentCleanupHandler<FakeDocument> sut = new(registry, _tagAssignments, _categoryAssignments);
        FakeDocument doc = new() { Id = Guid.NewGuid() };

        await sut.HandleAsync(new EntityDeletedEvent<FakeDocument>(doc), TestContext.Current.CancellationToken);

        await _tagAssignments.Received(1)
            .RemoveAllAssignmentsAsync(DocumentTargetType, doc.Id, Arg.Any<CancellationToken>());
        await _categoryAssignments.Received(1)
            .RemoveAllAssignmentsAsync(DocumentTargetType, doc.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TypeNotRegistered_NoOp()
    {
        TaggableTypeRegistry registry = new();
        // Registry intentionally empty — FakeUnregistered is not taggable.

        TaxonomyAssignmentCleanupHandler<FakeUnregistered> sut = new(registry, _tagAssignments, _categoryAssignments);
        FakeUnregistered entity = new() { Id = Guid.NewGuid() };

        await sut.HandleAsync(new EntityDeletedEvent<FakeUnregistered>(entity), TestContext.Current.CancellationToken);

        await _tagAssignments.DidNotReceiveWithAnyArgs()
            .RemoveAllAssignmentsAsync(default!, default, TestContext.Current.CancellationToken);
        await _categoryAssignments.DidNotReceiveWithAnyArgs()
            .RemoveAllAssignmentsAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_NullEvent_Throws()
    {
        TaggableTypeRegistry registry = new();
        TaxonomyAssignmentCleanupHandler<FakeDocument> sut = new(registry, _tagAssignments, _categoryAssignments);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            sut.HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
