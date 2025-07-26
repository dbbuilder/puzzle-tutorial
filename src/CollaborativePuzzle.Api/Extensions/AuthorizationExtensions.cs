using CollaborativePuzzle.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CollaborativePuzzle.Api.Extensions;

/// <summary>
/// Extension methods for configuring authorization
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds role-based authorization policies and handlers
    /// </summary>
    public static IServiceCollection AddRoleBasedAuthorization(this IServiceCollection services)
    {
        // Register authorization handlers
        services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, PuzzleOwnerAuthorizationHandler>();

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Role-based policies
            options.AddPolicy(RoleBasedPolicies.AdminPolicyName, RoleBasedPolicies.AdminPolicy);
            options.AddPolicy(RoleBasedPolicies.UserPolicyName, RoleBasedPolicies.UserPolicy);
            options.AddPolicy(RoleBasedPolicies.PlayerPolicyName, RoleBasedPolicies.PlayerPolicy);

            // Resource-based policies
            options.AddPolicy(PuzzlePolicies.PuzzleOwnerPolicyName, PuzzlePolicies.PuzzleOwnerPolicy);

            // Default policy
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // Fallback policy (applies to all endpoints without explicit authorization)
            options.FallbackPolicy = null; // Allow anonymous by default
        });

        return services;
    }
}