using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.DTOs;

/// <summary>
/// Request to login with an Azure AD B2C token
/// </summary>
public class AzureAdB2CLoginRequest
{
    [Required]
    public string B2CToken { get; set; } = default!;
}

/// <summary>
/// Request to handle Azure AD B2C callback
/// </summary>
public class AzureAdB2CCallbackRequest
{
    [Required]
    public string Code { get; set; } = default!;
    
    public string? State { get; set; }
    
    [Required]
    public string RedirectUri { get; set; } = default!;
}

/// <summary>
/// Response for Azure AD B2C authentication
/// </summary>
public class AzureAdB2CAuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
}