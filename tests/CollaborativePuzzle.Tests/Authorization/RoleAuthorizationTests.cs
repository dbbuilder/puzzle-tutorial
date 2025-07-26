using CollaborativePuzzle.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CollaborativePuzzle.Tests.Authorization;

public class RoleAuthorizationTests
{
    [Fact]
    public async Task RequireRoleAttribute_WithUserInRole_ShouldAuthorize()
    {
        // Arrange
        var requirement = new RoleRequirement("Admin");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "Test"));

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        var handler = new RoleAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task RequireRoleAttribute_WithUserNotInRole_ShouldNotAuthorize()
    {
        // Arrange
        var requirement = new RoleRequirement("Admin");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User")
        }, "Test"));

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        var handler = new RoleAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task RequireRoleAttribute_WithMultipleRoles_ShouldAuthorizeIfUserHasAnyRole()
    {
        // Arrange
        var requirement = new RoleRequirement("Admin", "Moderator");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Moderator")
        }, "Test"));

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        var handler = new RoleAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task RequireRoleAttribute_WithUnauthenticatedUser_ShouldNotAuthorize()
    {
        // Arrange
        var requirement = new RoleRequirement("Admin");
        var user = new ClaimsPrincipal(); // Unauthenticated

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            null);

        var handler = new RoleAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public void RoleBasedAuthorizationPolicies_ShouldBeConfiguredCorrectly()
    {
        // Arrange & Act
        var adminPolicy = RoleBasedPolicies.AdminPolicy;
        var userPolicy = RoleBasedPolicies.UserPolicy;
        var playerPolicy = RoleBasedPolicies.PlayerPolicy;

        // Assert
        adminPolicy.Should().NotBeNull();
        adminPolicy.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<RoleRequirement>()
            .Which.AllowedRoles.Should().Contain("Admin");

        userPolicy.Should().NotBeNull();
        userPolicy.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<RoleRequirement>()
            .Which.AllowedRoles.Should().Contain("User");

        playerPolicy.Should().NotBeNull();
        playerPolicy.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<RoleRequirement>()
            .Which.AllowedRoles.Should().Contain("Player");
    }

    [Fact]
    public async Task PuzzleOwnerRequirement_WithOwner_ShouldAuthorize()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var userId = "user123";
        var requirement = new PuzzleOwnerRequirement();
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "Test"));

        var routeData = new RouteData();
        routeData.Values["puzzleId"] = puzzleId.ToString();

        var httpContext = new DefaultHttpContext
        {
            User = user
        };
        httpContext.Request.RouteValues = routeData.Values;

        var resource = new AuthorizationFilterContext(
            new ActionContext(httpContext, routeData, new ActionDescriptor()),
            new List<IFilterMetadata>());

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource);

        var puzzleService = new Mock<IPuzzleService>();
        puzzleService.Setup(x => x.IsPuzzleOwnerAsync(puzzleId, userId))
            .ReturnsAsync(true);

        var handler = new PuzzleOwnerAuthorizationHandler(puzzleService.Object);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task PuzzleOwnerRequirement_WithNonOwner_ShouldNotAuthorize()
    {
        // Arrange
        var puzzleId = Guid.NewGuid();
        var userId = "user123";
        var requirement = new PuzzleOwnerRequirement();
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "Test"));

        var routeData = new RouteData();
        routeData.Values["puzzleId"] = puzzleId.ToString();

        var httpContext = new DefaultHttpContext
        {
            User = user
        };
        httpContext.Request.RouteValues = routeData.Values;

        var resource = new AuthorizationFilterContext(
            new ActionContext(httpContext, routeData, new ActionDescriptor()),
            new List<IFilterMetadata>());

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource);

        var puzzleService = new Mock<IPuzzleService>();
        puzzleService.Setup(x => x.IsPuzzleOwnerAsync(puzzleId, userId))
            .ReturnsAsync(false);

        var handler = new PuzzleOwnerAuthorizationHandler(puzzleService.Object);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }
}

// Mock interface for testing
public interface IPuzzleService
{
    Task<bool> IsPuzzleOwnerAsync(Guid puzzleId, string userId);
}