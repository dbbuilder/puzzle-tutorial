namespace CollaborativePuzzle.Api.Authentication;

/// <summary>
/// Configuration settings for Azure AD B2C authentication
/// </summary>
public class AzureAdB2CSettings
{
    /// <summary>
    /// The Azure AD B2C instance (e.g., https://yourtenant.b2clogin.com)
    /// </summary>
    public string Instance { get; set; } = string.Empty;
    
    /// <summary>
    /// The Azure AD B2C domain (e.g., yourtenant.onmicrosoft.com)
    /// </summary>
    public string Domain { get; set; } = string.Empty;
    
    /// <summary>
    /// The Azure AD B2C tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
    
    /// <summary>
    /// The client ID (application ID) of the registered application
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// The client secret for the registered application (for confidential clients)
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
    
    /// <summary>
    /// The redirect URI for OAuth2 callbacks
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
    
    /// <summary>
    /// The sign-up/sign-in user flow name
    /// </summary>
    public string SignUpSignInPolicyId { get; set; } = string.Empty;
    
    /// <summary>
    /// The reset password user flow name
    /// </summary>
    public string ResetPasswordPolicyId { get; set; } = string.Empty;
    
    /// <summary>
    /// The edit profile user flow name
    /// </summary>
    public string EditProfilePolicyId { get; set; } = string.Empty;
    
    /// <summary>
    /// The default policy to use
    /// </summary>
    public string DefaultPolicy => SignUpSignInPolicyId;
    
    /// <summary>
    /// The authority URL for the default policy
    /// </summary>
    public string Authority => $"{Instance}/{Domain}/{DefaultPolicy}/v2.0";
    
    /// <summary>
    /// The metadata address for OpenID configuration
    /// </summary>
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
    
    /// <summary>
    /// Whether to validate the issuer
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;
    
    /// <summary>
    /// Valid audiences for token validation
    /// </summary>
    public string[] ValidAudiences => new[] { ClientId };
}