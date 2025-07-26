using Microsoft.AspNetCore.Authorization;

namespace CollaborativePuzzle.Api.Authorization;

/// <summary>
/// Requires the user to have Admin role
/// </summary>
public class RequireAdminAttribute : AuthorizeAttribute
{
    public RequireAdminAttribute()
    {
        Policy = RoleBasedPolicies.AdminPolicyName;
    }
}

/// <summary>
/// Requires the user to have User role (or Admin)
/// </summary>
public class RequireUserAttribute : AuthorizeAttribute
{
    public RequireUserAttribute()
    {
        Policy = RoleBasedPolicies.UserPolicyName;
    }
}

/// <summary>
/// Requires the user to have Player role (or User/Admin)
/// </summary>
public class RequirePlayerAttribute : AuthorizeAttribute
{
    public RequirePlayerAttribute()
    {
        Policy = RoleBasedPolicies.PlayerPolicyName;
    }
}

/// <summary>
/// Requires the user to be the owner of the puzzle
/// </summary>
public class RequirePuzzleOwnerAttribute : AuthorizeAttribute
{
    public RequirePuzzleOwnerAttribute()
    {
        Policy = PuzzlePolicies.PuzzleOwnerPolicyName;
    }
}