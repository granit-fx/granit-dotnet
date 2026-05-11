using System.Security.Claims;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Authorization;
using Granit.Documents.Permissions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Authorization;

public sealed class DocumentTaggablePermissionResolverTests
{
    private static readonly string DocumentTargetType = typeof(Document).FullName!;

    [Fact]
    public async Task IsAuthorizedAsync_NonDocumentTarget_AllowsThrough()
    {
        IHttpContextAccessor http = Substitute.For<IHttpContextAccessor>();
        DocumentTaggablePermissionResolver sut = new(http);

        bool authorized = await sut.IsAuthorizedAsync(
            "Granit.Parties.Domain.Party", Guid.NewGuid(), TestContext.Current.CancellationToken);

        authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_DocumentTarget_NoUser_Denies()
    {
        IHttpContextAccessor http = Substitute.For<IHttpContextAccessor>();
        http.HttpContext.Returns((HttpContext?)null);
        DocumentTaggablePermissionResolver sut = new(http);

        bool authorized = await sut.IsAuthorizedAsync(
            DocumentTargetType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        authorized.ShouldBeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_DocumentTarget_AuthenticatedWithoutManageClaim_Denies()
    {
        DocumentTaggablePermissionResolver sut = CreateSutWithUser(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permission", DocumentsPermissions.Documents.Read)],
                authenticationType: "Test")));

        bool authorized = await sut.IsAuthorizedAsync(
            DocumentTargetType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        authorized.ShouldBeFalse();
    }

    [Fact]
    public async Task IsAuthorizedAsync_DocumentTarget_AuthenticatedWithManageClaim_Allows()
    {
        DocumentTaggablePermissionResolver sut = CreateSutWithUser(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permission", DocumentsPermissions.Documents.Manage)],
                authenticationType: "Test")));

        bool authorized = await sut.IsAuthorizedAsync(
            DocumentTargetType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_DocumentTarget_AnonymousIdentity_Denies()
    {
        DocumentTaggablePermissionResolver sut = CreateSutWithUser(new ClaimsPrincipal(new ClaimsIdentity()));

        bool authorized = await sut.IsAuthorizedAsync(
            DocumentTargetType, Guid.NewGuid(), TestContext.Current.CancellationToken);

        authorized.ShouldBeFalse();
    }

    private static DocumentTaggablePermissionResolver CreateSutWithUser(ClaimsPrincipal user)
    {
        DefaultHttpContext httpContext = new() { User = user };
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new DocumentTaggablePermissionResolver(accessor);
    }
}
