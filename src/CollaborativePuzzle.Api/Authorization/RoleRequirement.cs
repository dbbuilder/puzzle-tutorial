using Microsoft.AspNetCore.Authorization;

namespace CollaborativePuzzle.Api.Authorization;

/// <summary>
/// Authorization requirement for role-based access
/// </summary>
public class RoleRequirement : IAuthorizationRequirement
{
    public RoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles ?? throw new ArgumentNullException(nameof(allowedRoles));
    }

    public string[] AllowedRoles { get; }
}

/// <summary>
/// Handler for role-based authorization
/// </summary>
public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RoleRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return Task.CompletedTask;
        }

        if (requirement.AllowedRoles.Any(role => context.User.IsInRole(role)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Predefined role-based authorization policies
/// </summary>
public static class RoleBasedPolicies
{
    public const string AdminPolicyName = "RequireAdminRole";
    public const string UserPolicyName = "RequireUserRole";
    public const string PlayerPolicyName = "RequirePlayerRole";

    public static AuthorizationPolicy AdminPolicy =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RoleRequirement("Admin"))
            .Build();

    public static AuthorizationPolicy UserPolicy =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RoleRequirement("User", "Admin"))
            .Build();

    public static AuthorizationPolicy PlayerPolicy =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RoleRequirement("Player", "User", "Admin"))
            .Build();
}