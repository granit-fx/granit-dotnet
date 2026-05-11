using Granit.Authorization;
using Granit.Documents.Permissions;
using Granit.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Permissions;

/// <summary>
/// Unit tests for <see cref="DocumentsPermissionDefinitionProvider"/> — verifies the
/// Documents permission group registers with the expected resource / action surface
/// (F12.1).
/// </summary>
public sealed class DocumentsPermissionDefinitionProviderTests
{
    private static (DocumentsPermissionDefinitionProvider provider, IPermissionDefinitionContext context, PermissionGroup group) BuildHarness()
    {
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(DocumentsPermissions.GroupName);
        context.AddGroup(DocumentsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);
        return (new DocumentsPermissionDefinitionProvider(), context, group);
    }

    [Fact]
    public void DefinePermissions_RegistersDocumentsGroup()
    {
        (DocumentsPermissionDefinitionProvider provider, IPermissionDefinitionContext context, _) = BuildHarness();

        provider.DefinePermissions(context);

        context.Received(1).AddGroup(DocumentsPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Theory]
    [InlineData("Documents.Folders.Read")]
    [InlineData("Documents.Folders.Manage")]
    [InlineData("Documents.Documents.Read")]
    [InlineData("Documents.Documents.Manage")]
    [InlineData("Documents.Shares.Read")]
    [InlineData("Documents.Shares.Manage")]
    [InlineData("Documents.Tags.Read")]
    [InlineData("Documents.Tags.Manage")]
    [InlineData("Documents.Quotas.Read")]
    public void DefinePermissions_RegistersExpectedPermission(string permissionName)
    {
        (DocumentsPermissionDefinitionProvider provider, IPermissionDefinitionContext context, PermissionGroup group) = BuildHarness();

        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == permissionName);
    }

    [Fact]
    public void DefinePermissions_RegistersExactlyNinePermissions()
    {
        (DocumentsPermissionDefinitionProvider provider, IPermissionDefinitionContext context, PermissionGroup group) = BuildHarness();

        provider.DefinePermissions(context);

        group.Permissions.Count.ShouldBe(9);
    }
}
