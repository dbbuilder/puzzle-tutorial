using Microsoft.AspNetCore.Authorization;

namespace CollaborativePuzzle.Api.Authorization;

/// <summary>
/// Authorization requirement for API key scopes
/// </summary>
public class ApiKeyScopeRequirement : IAuthorizationRequirement
{
    public string RequiredScope { get; }

    public ApiKeyScopeRequirement(string requiredScope)
    {
        RequiredScope = requiredScope;
    }
}

/// <summary>
/// Authorization handler for API key scope requirements
/// </summary>
public class ApiKeyScopeAuthorizationHandler : AuthorizationHandler<ApiKeyScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyScopeRequirement requirement)
    {
        // Check if user is authenticated via API key
        var authMethod = context.User.FindFirst("AuthenticationMethod")?.Value;
        if (authMethod != "ApiKey")
        {
            // Not API key authentication, let other handlers decide
            return Task.CompletedTask;
        }

        // Check if user has the required scope
        var scopes = context.User.FindAll("scope").Select(c => c.Value);
        if (scopes.Contains(requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, 
                $"API key does not have the required scope: {requirement.RequiredScope}"));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Attribute to require specific API key scopes
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireApiScopeAttribute : AuthorizeAttribute
{
    public RequireApiScopeAttribute(string scope) : base($"ApiScope:{scope}")
    {
        Scope = scope;
    }

    public string Scope { get; }
}