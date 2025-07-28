using System.Text;
using CollaborativePuzzle.Api.Authentication;
using CollaborativePuzzle.Api.Authorization;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Infrastructure.Services;
using JwtSettings = CollaborativePuzzle.Api.Extensions.JwtSettings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CollaborativePuzzle.Api.Extensions;

/// <summary>
/// Extension methods for configuring authentication and authorization
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds JWT authentication and Azure AD B2C support
    /// </summary>
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure JWT settings
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        
        // Configure Azure AD B2C settings
        var azureAdB2CSettings = configuration.GetSection("AzureAdB2C").Get<AzureAdB2CSettings>();
        if (azureAdB2CSettings != null)
        {
            services.Configure<AzureAdB2CSettings>(configuration.GetSection("AzureAdB2C"));
        }
        
        // Add authentication services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IUserService, UserService>();
        
        // Add Azure AD B2C service if configured
        if (azureAdB2CSettings != null && !string.IsNullOrEmpty(azureAdB2CSettings.ClientId))
        {
            services.AddScoped<IExternalAuthenticationService, AzureAdB2CAuthenticationService>();
        }
        
        // Configure JWT authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(5)
            };
            
            // Support for SignalR authentication via query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    
                    // If the request is for our hub...
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/puzzlehub") ||
                         path.StartsWithSegments("/webrtchub")))
                    {
                        // Read the token out of the query string
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds custom authorization policies
    /// </summary>
    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    {
        // Add authorization handlers
        services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, PuzzleOwnerAuthorizationHandler>();
        
        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Role-based policies
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
            options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User", "Admin"));
            options.AddPolicy("RequirePlayerRole", policy => policy.RequireRole("Player", "User", "Admin"));
            
            // Custom policies
            options.AddPolicy(PuzzlePolicies.PuzzleOwnerPolicyName, PuzzlePolicies.PuzzleOwnerPolicy);
        });
        
        return services;
    }
}

/// <summary>
/// JWT configuration settings
/// </summary>
public class JwtSettings
{
    public string SecretKey { get; set; } = "DefaultSecretKeyForDevelopment_ChangeInProduction!";
    public string Issuer { get; set; } = "CollaborativePuzzle.Api";
    public string Audience { get; set; } = "CollaborativePuzzle.Client";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}