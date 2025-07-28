namespace CollaborativePuzzle.Core.Models;

/// <summary>
/// Result of external authentication
/// </summary>
public class ExternalAuthenticationResult
{
    public bool Success { get; set; }
    public User? User { get; set; }
    public string? Token { get; set; }
    public IEnumerable<string> Roles { get; set; } = new List<string>();
    public string? Error { get; set; }
    
    public static ExternalAuthenticationResult CreateSuccess(User user, string token, IEnumerable<string> roles)
    {
        return new ExternalAuthenticationResult
        {
            Success = true,
            User = user,
            Token = token,
            Roles = roles
        };
    }
    
    public static ExternalAuthenticationResult CreateFailure(string error)
    {
        return new ExternalAuthenticationResult
        {
            Success = false,
            Error = error
        };
    }
}