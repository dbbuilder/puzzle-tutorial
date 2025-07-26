using CollaborativePuzzle.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace CollaborativePuzzle.Api.Authorization;

/// <summary>
/// Authorization requirement for puzzle ownership
/// </summary>
public class PuzzleOwnerRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Handler for puzzle owner authorization
/// </summary>
public class PuzzleOwnerAuthorizationHandler : AuthorizationHandler<PuzzleOwnerRequirement>
{
    private readonly IPuzzleRepository _puzzleRepository;

    public PuzzleOwnerAuthorizationHandler(IPuzzleRepository puzzleRepository)
    {
        _puzzleRepository = puzzleRepository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PuzzleOwnerRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Try to get puzzle ID from route data
        if (context.Resource is AuthorizationFilterContext filterContext)
        {
            if (filterContext.HttpContext.Request.RouteValues.TryGetValue("puzzleId", out var puzzleIdValue) &&
                Guid.TryParse(puzzleIdValue?.ToString(), out var puzzleId))
            {
                var puzzle = await _puzzleRepository.GetPuzzleAsync(puzzleId);
                if (puzzle != null && puzzle.CreatedByUserId.ToString() == userId)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}

/// <summary>
/// Authorization policies for puzzle-related operations
/// </summary>
public static class PuzzlePolicies
{
    public const string PuzzleOwnerPolicyName = "RequirePuzzleOwner";

    public static AuthorizationPolicy PuzzleOwnerPolicy =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PuzzleOwnerRequirement())
            .Build();
}