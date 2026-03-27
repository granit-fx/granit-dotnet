// =============================================================================
// Tests - PermissionAuthorizationHandler
// =============================================================================
// Vérifie que le handler appelle Succeed lorsque la permission est accordée,
// et ne le fait pas lorsqu'elle est refusée.
// =============================================================================

using System.Security.Claims;
using Granit.Authorization.Abstractions;
using Granit.Authorization.Authorization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleAsync_PermissionGranted_ContextSucceeds()
    {
        // Arrange
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoices.Delete", Arg.Any<CancellationToken>()).Returns(true);

        PermissionAuthorizationHandler handler = new(checker);
        PermissionRequirement requirement = new("Invoices.Delete");
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_PermissionDenied_ContextDoesNotSucceed()
    {
        // Arrange
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoices.Delete", Arg.Any<CancellationToken>()).Returns(false);

        PermissionAuthorizationHandler handler = new(checker);
        PermissionRequirement requirement = new("Invoices.Delete");
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    // =========================================================================
    // HandleAsync — checker interaction
    // =========================================================================

    [Fact]
    public async Task HandleAsync_CallsCheckerWithCorrectPermissionName()
    {
        // Arrange
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("BlobStorage.Blobs.Read", Arg.Any<CancellationToken>()).Returns(true);

        PermissionAuthorizationHandler handler = new(checker);
        PermissionRequirement requirement = new("BlobStorage.Blobs.Read");
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        await checker.Received(1).IsGrantedAsync("BlobStorage.Blobs.Read", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithResource_StillChecksPermission()
    {
        // Arrange
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoices.Read", Arg.Any<CancellationToken>()).Returns(true);

        PermissionAuthorizationHandler handler = new(checker);
        PermissionRequirement requirement = new("Invoices.Read");
        AuthorizationHandlerContext context = new(
            [requirement],
            new ClaimsPrincipal(),
            resource: "some-resource");

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_MultipleRequirements_OnlyHandlesPermissionRequirement()
    {
        // Arrange
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoices.Delete", Arg.Any<CancellationToken>()).Returns(true);

        PermissionAuthorizationHandler handler = new(checker);
        PermissionRequirement requirement = new("Invoices.Delete");

        // Include another requirement type that the handler does not handle
        var otherRequirement = new Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement();

        AuthorizationHandlerContext context = new(
            [requirement, otherRequirement],
            new ClaimsPrincipal(),
            resource: null);

        // Act
        await handler.HandleAsync(context);

        // Assert — the handler should have called Succeed only for the PermissionRequirement
        await checker.Received(1).IsGrantedAsync("Invoices.Delete", Arg.Any<CancellationToken>());
    }
}
