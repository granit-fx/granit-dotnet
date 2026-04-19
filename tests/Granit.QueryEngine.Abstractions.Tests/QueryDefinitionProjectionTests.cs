using System.Linq.Expressions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryDefinitionProjectionTests
{
    [Fact]
    public void Builder_ProjectTo_stores_expression_and_type()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();
        Expression<Func<SampleEntity, SampleDto>> projection = e => new SampleDto(e.Id, e.Name);

        builder.ProjectTo(projection);

        QueryDefinitionWithProjection definition = new(projection);
        definition.GetProjectionType().ShouldBe(typeof(SampleDto));
        definition.GetProjectionExpression().ShouldNotBeNull();
    }

    [Fact]
    public void Builder_ProjectTo_called_twice_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();
        builder.ProjectTo<SampleDto>(e => new SampleDto(e.Id, e.Name));

        Should.Throw<InvalidOperationException>(
            () => builder.ProjectTo<SampleDto>(e => new SampleDto(e.Id, e.Name)));
    }

    [Fact]
    public void Builder_ProjectTo_null_projection_throws()
    {
        QueryDefinitionBuilder<SampleEntity> builder = new();

        Should.Throw<ArgumentNullException>(
            () => builder.ProjectTo<SampleDto>(null!));
    }

    [Fact]
    public void Definition_accessors_return_null_when_no_projection_declared()
    {
        SampleEntityQueryDefinition definition = new();

        definition.GetProjectionType().ShouldBeNull();
        definition.GetProjectionExpression().ShouldBeNull();
    }

    [Fact]
    public void Definition_accessors_return_declared_projection()
    {
        Expression<Func<SampleEntity, SampleDto>> projection = e => new SampleDto(e.Id, e.Name);
        QueryDefinitionWithProjection definition = new(projection);

        definition.GetProjectionType().ShouldBe(typeof(SampleDto));
        definition.GetProjectionExpression().ShouldNotBeNull();
        definition.GetProjectionExpression()!.Parameters.Count.ShouldBe(1);
        definition.GetProjectionExpression()!.ReturnType.ShouldBe(typeof(SampleDto));
    }

    private sealed class SampleEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }

    private sealed record SampleDto(Guid Id, string Name);

    private sealed class SampleEntityQueryDefinition : QueryDefinition<SampleEntity>
    {
        public override string Name => "Sample.Entities";

        protected override void Configure(QueryDefinitionBuilder<SampleEntity> builder) =>
            builder.Column(e => e.Name);
    }

    private sealed class QueryDefinitionWithProjection : QueryDefinition<SampleEntity>
    {
        private readonly Expression<Func<SampleEntity, SampleDto>> _projection;

        public QueryDefinitionWithProjection(Expression<Func<SampleEntity, SampleDto>> projection) =>
            _projection = projection;

        public override string Name => "Sample.Entities.Projected";

        protected override void Configure(QueryDefinitionBuilder<SampleEntity> builder) =>
            builder
                .Column(e => e.Id)
                .Column(e => e.Name)
                .ProjectTo(_projection);
    }
}
