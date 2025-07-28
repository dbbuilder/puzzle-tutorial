using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces;

/// <summary>
/// Interface for external authentication providers (e.g., Azure AD B2C, OAuth providers)
/// </summary>
public interface IExternalAuthenticationService
{
    /// <summary>
    /// Validates an external authentication token
    /// </summary>
    /// <param name="token">The external provider's token</param>
    /// <returns>Authentication result with user information</returns>
    Task<ExternalAuthenticationResult> ValidateTokenAsync(string token);
    
    /// <summary>
    /// Exchanges an authorization code for tokens
    /// </summary>
    /// <param name="code">The authorization code</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request</param>
    /// <returns>Authentication result with user information</returns>
    Task<ExternalAuthenticationResult> ExchangeCodeAsync(string code, string redirectUri);
    
    /// <summary>
    /// Gets the login URL for the external provider
    /// </summary>
    /// <param name="redirectUri">The URI to redirect to after login</param>
    /// <param name="state">Optional state parameter for CSRF protection</param>
    /// <returns>The login URL</returns>
    string GetLoginUrl(string redirectUri, string? state = null);
    
    /// <summary>
    /// Gets the logout URL for the external provider
    /// </summary>
    /// <param name="redirectUri">The URI to redirect to after logout</param>
    /// <returns>The logout URL</returns>
    string GetLogoutUrl(string redirectUri);
}