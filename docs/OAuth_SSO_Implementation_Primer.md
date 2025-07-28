# OAuth & SSO Implementation Primer

## Complete Guide for Multi-Provider Authentication with Cost Optimization

### Table of Contents
1. [Understanding OAuth 2.0 & OpenID Connect](#understanding-oauth-20--openid-connect)
2. [Architecture Overview](#architecture-overview)
3. [Basic Email/Password Authentication](#basic-emailpassword-authentication)
4. [Implementing OAuth Providers](#implementing-oauth-providers)
5. [SSO Integration](#sso-integration)
6. [Account Linking & Migration](#account-linking--migration)
7. [Cost Optimization Strategies](#cost-optimization-strategies)
8. [Security Best Practices](#security-best-practices)
9. [Implementation Roadmap](#implementation-roadmap)
10. [Monitoring & Maintenance](#monitoring--maintenance)

## Understanding OAuth 2.0 & OpenID Connect

### Core Concepts

```yaml
OAuth 2.0:
  Purpose: Authorization (what you can access)
  Grants:
    - Authorization Code: Web apps
    - Implicit: Deprecated
    - Client Credentials: M2M
    - Resource Owner Password: Legacy
    - Device Code: Smart TVs, IoT
    - PKCE: Mobile/SPA apps

OpenID Connect (OIDC):
  Purpose: Authentication (who you are)
  Built on: OAuth 2.0
  Additions:
    - ID Token (JWT)
    - UserInfo endpoint
    - Discovery endpoint
    - Dynamic registration
```

### Authentication Flow

```
┌─────────┐          ┌──────────┐         ┌──────────────┐
│  User   │─────────▶│ Your App │────────▶│   Provider   │
└─────────┘          └──────────┘         │  (Google,    │
     ▲                     │               │  GitHub...)  │
     │                     │               └──────────────┘
     │                     ▼                       │
     │            ┌───────────────┐               │
     └────────────│ Authorization │◀──────────────┘
                  │    Server      │
                  └───────────────┘
```

## Architecture Overview

### Complete Authentication System

```csharp
public class AuthenticationArchitecture
{
    // Core components needed
    public interface IAuthenticationSystem
    {
        // Local authentication
        Task<AuthResult> AuthenticateLocal(string email, string password);
        Task<AuthResult> RegisterLocal(string email, string password);
        
        // OAuth authentication
        Task<AuthResult> AuthenticateOAuth(string provider, string code);
        Task<IEnumerable<string>> GetConfiguredProviders();
        
        // SSO authentication
        Task<AuthResult> AuthenticateSAML(string samlResponse);
        Task<AuthResult> AuthenticateOIDC(string idToken);
        
        // Account management
        Task<LinkResult> LinkAccount(User user, string provider, string externalId);
        Task<User> FindOrCreateUser(ExternalUserInfo info);
        
        // Token management
        Task<TokenPair> GenerateTokens(User user);
        Task<TokenPair> RefreshTokens(string refreshToken);
        Task RevokeTokens(string userId);
    }
}
```

### Database Schema

```sql
-- Users table
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) UNIQUE,
    EmailConfirmed BIT DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NULL, -- NULL for OAuth-only users
    Username NVARCHAR(256) UNIQUE,
    TwoFactorEnabled BIT DEFAULT 0,
    LockoutEnd DATETIMEOFFSET NULL,
    LockoutEnabled BIT DEFAULT 1,
    AccessFailedCount INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2 NULL,
    IsActive BIT DEFAULT 1
);

-- External logins
CREATE TABLE UserLogins (
    LoginProvider NVARCHAR(128),
    ProviderKey NVARCHAR(128),
    UserId UNIQUEIDENTIFIER,
    ProviderDisplayName NVARCHAR(256),
    PRIMARY KEY (LoginProvider, ProviderKey),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- User claims from providers
CREATE TABLE UserClaims (
    Id INT IDENTITY PRIMARY KEY,
    UserId UNIQUEIDENTIFIER,
    ClaimType NVARCHAR(256),
    ClaimValue NVARCHAR(MAX),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Refresh tokens
CREATE TABLE RefreshTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER,
    Token NVARCHAR(88) UNIQUE,
    ExpiresAt DATETIME2,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    RevokedAt DATETIME2 NULL,
    ReplacedByToken NVARCHAR(88) NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Audit log
CREATE TABLE AuthenticationLog (
    Id BIGINT IDENTITY PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NULL,
    Event NVARCHAR(50),
    Provider NVARCHAR(50),
    IpAddress NVARCHAR(45),
    UserAgent NVARCHAR(512),
    Success BIT,
    FailureReason NVARCHAR(256) NULL,
    Timestamp DATETIME2 DEFAULT GETUTCDATE()
);
```

## Basic Email/Password Authentication

### Implementation with ASP.NET Core Identity

```csharp
// Program.cs configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 6;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    
    // User settings
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = 
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Custom password hasher for better security
public class ArgonPasswordHasher<TUser> : IPasswordHasher<TUser> 
    where TUser : class
{
    public string HashPassword(TUser user, string password)
    {
        // Use Argon2id for password hashing
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 8,
            Iterations = 4,
            MemorySize = 1024 * 128 // 128 MB
        };
        
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}
```

### Registration and Login Endpoints

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto model)
    {
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = false
        };
        
        var result = await _userManager.CreateAsync(user, model.Password);
        
        if (result.Succeeded)
        {
            // Send confirmation email
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _emailService.SendConfirmationEmail(user.Email, code);
            
            // Log registration
            await _authLogger.LogRegistration(user.Id, HttpContext);
            
            return Ok(new { message = "Registration successful. Please check your email." });
        }
        
        return BadRequest(result.Errors);
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            await _authLogger.LogFailedLogin(model.Email, "User not found", HttpContext);
            return Unauthorized("Invalid credentials");
        }
        
        if (!user.EmailConfirmed)
        {
            return Unauthorized("Please confirm your email first");
        }
        
        var result = await _signInManager.CheckPasswordSignInAsync(
            user, 
            model.Password, 
            lockoutOnFailure: true);
            
        if (result.Succeeded)
        {
            var tokens = await GenerateTokens(user);
            await _authLogger.LogSuccessfulLogin(user.Id, "Local", HttpContext);
            
            return Ok(tokens);
        }
        
        if (result.IsLockedOut)
        {
            return Unauthorized("Account locked. Try again later.");
        }
        
        return Unauthorized("Invalid credentials");
    }
}
```

## Implementing OAuth Providers

### Multi-Provider Configuration

```csharp
// Program.cs - Configure all providers
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
})
.AddGoogle(options =>
{
    options.ClientId = configuration["OAuth:Google:ClientId"];
    options.ClientSecret = configuration["OAuth:Google:ClientSecret"];
    options.CallbackPath = "/auth/signin-google";
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.SaveTokens = true;
})
.AddMicrosoftAccount(options =>
{
    options.ClientId = configuration["OAuth:Microsoft:ClientId"];
    options.ClientSecret = configuration["OAuth:Microsoft:ClientSecret"];
    options.CallbackPath = "/auth/signin-microsoft";
    options.SaveTokens = true;
})
.AddGitHub(options =>
{
    options.ClientId = configuration["OAuth:GitHub:ClientId"];
    options.ClientSecret = configuration["OAuth:GitHub:ClientSecret"];
    options.CallbackPath = "/auth/signin-github";
    options.Scope.Add("user:email");
    options.SaveTokens = true;
})
.AddApple(options =>
{
    options.ClientId = configuration["OAuth:Apple:ClientId"];
    options.TeamId = configuration["OAuth:Apple:TeamId"];
    options.KeyId = configuration["OAuth:Apple:KeyId"];
    options.PrivateKey = async (keyId) =>
    {
        // Load Apple private key from secure storage
        return await _keyVault.GetApplePrivateKey(keyId);
    };
    options.SaveTokens = true;
})
.AddFacebook(options =>
{
    options.AppId = configuration["OAuth:Facebook:AppId"];
    options.AppSecret = configuration["OAuth:Facebook:AppSecret"];
    options.CallbackPath = "/auth/signin-facebook";
    options.Fields.Add("email");
    options.Fields.Add("name");
    options.SaveTokens = true;
});
```

### OAuth Callback Handler

```csharp
public class OAuthController : Controller
{
    [HttpGet("signin/{provider}")]
    public IActionResult SignIn(string provider, string returnUrl = "/")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(SignInCallback), new { returnUrl }),
            Items = { ["scheme"] = provider }
        };
        
        return Challenge(properties, provider);
    }
    
    [HttpGet("signin-callback")]
    public async Task<IActionResult> SignInCallback(string returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
            
        if (!result.Succeeded)
        {
            return RedirectToAction("Login", "Auth", 
                new { error = "external_login_failure" });
        }
        
        var externalUser = result.Principal;
        var provider = result.Properties.Items["scheme"];
        var providerKey = externalUser.FindFirstValue(ClaimTypes.NameIdentifier);
        
        // Find or create user
        var user = await _authService.FindOrCreateUser(new ExternalUserInfo
        {
            Provider = provider,
            ProviderKey = providerKey,
            Email = externalUser.FindFirstValue(ClaimTypes.Email),
            Name = externalUser.FindFirstValue(ClaimTypes.Name),
            Claims = externalUser.Claims
        });
        
        // Generate our own tokens
        var tokens = await GenerateTokens(user);
        
        // Redirect with tokens (or set cookies)
        return Redirect($"{returnUrl}?token={tokens.AccessToken}");
    }
}
```

### Provider-Specific Handling

```csharp
public class ProviderSpecificHandlers
{
    // Apple requires special handling
    public class AppleAuthHandler
    {
        public async Task<AppleTokenResponse> ValidateToken(string idToken)
        {
            // Apple uses JWT for ID tokens
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);
            
            // Validate against Apple's public keys
            var keys = await GetApplePublicKeys();
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = "https://appleid.apple.com",
                ValidAudience = _configuration["OAuth:Apple:ClientId"],
                IssuerSigningKeys = keys
            };
            
            var principal = handler.ValidateToken(
                idToken, 
                validationParameters, 
                out _);
                
            return new AppleTokenResponse
            {
                Sub = principal.FindFirst("sub")?.Value,
                Email = principal.FindFirst("email")?.Value,
                EmailVerified = bool.Parse(
                    principal.FindFirst("email_verified")?.Value ?? "false")
            };
        }
    }
    
    // GitHub doesn't return email in token
    public class GitHubAuthHandler
    {
        public async Task<string> GetPrimaryEmail(string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YourApp/1.0");
            
            var response = await client.GetAsync(
                "https://api.github.com/user/emails");
            var emails = await response.Content
                .ReadFromJsonAsync<List<GitHubEmail>>();
                
            return emails?.FirstOrDefault(e => e.Primary && e.Verified)?.Email;
        }
    }
}
```

## SSO Integration

### SAML 2.0 Implementation

```csharp
// Using Sustainsys.Saml2
builder.Services.AddAuthentication()
    .AddSaml2(options =>
    {
        options.SPOptions.EntityId = new EntityId("https://yourapp.com/saml");
        options.SPOptions.ReturnUrl = new Uri("https://yourapp.com/saml/acs");
        
        // Add identity providers
        options.IdentityProviders.Add(
            new IdentityProvider(
                new EntityId("https://idp.company.com"), 
                options.SPOptions)
            {
                MetadataLocation = "https://idp.company.com/metadata",
                LoadMetadata = true
            });
    });

// SAML Controller
[Route("saml")]
public class SamlController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string company)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/saml/callback",
            Items = { ["company"] = company }
        };
        
        return Challenge(properties, "Saml2");
    }
    
    [HttpPost("acs")]
    public async Task<IActionResult> AssertionConsumerService()
    {
        var result = await HttpContext.AuthenticateAsync("Saml2");
        
        if (result.Succeeded)
        {
            var user = await FindOrCreateSamlUser(result.Principal);
            var tokens = await GenerateTokens(user);
            
            return Redirect($"/app?token={tokens.AccessToken}");
        }
        
        return Redirect("/login?error=saml_failed");
    }
}
```

### Enterprise SSO with OIDC

```csharp
public class EnterpriseSSOService
{
    public void ConfigureEnterpriseSSO(
        IServiceCollection services, 
        IConfiguration configuration)
    {
        // Dynamic provider registration
        var providers = configuration
            .GetSection("SSO:Providers")
            .Get<List<SSOProvider>>();
            
        foreach (var provider in providers)
        {
            services.AddAuthentication()
                .AddOpenIdConnect(provider.Name, options =>
                {
                    options.Authority = provider.Authority;
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.CallbackPath = $"/auth/callback/{provider.Name}";
                    
                    // Map claims
                    options.ClaimActions.MapJsonKey(
                        ClaimTypes.Email, 
                        provider.EmailClaim ?? "email");
                    options.ClaimActions.MapJsonKey(
                        ClaimTypes.Name, 
                        provider.NameClaim ?? "name");
                        
                    // Custom scopes
                    foreach (var scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
        }
    }
}
```

## Account Linking & Migration

### Account Linking Strategy

```csharp
public class AccountLinkingService
{
    public async Task<LinkResult> LinkAccounts(
        User existingUser, 
        ExternalLoginInfo newLogin)
    {
        // Check if already linked
        var existingLink = await _context.UserLogins
            .FirstOrDefaultAsync(l => 
                l.LoginProvider == newLogin.LoginProvider &&
                l.ProviderKey == newLogin.ProviderKey);
                
        if (existingLink != null)
        {
            if (existingLink.UserId == existingUser.Id)
            {
                return LinkResult.AlreadyLinked();
            }
            
            return LinkResult.LinkedToAnotherAccount();
        }
        
        // Check email match for auto-linking
        var email = newLogin.Principal.FindFirstValue(ClaimTypes.Email);
        if (existingUser.Email != email)
        {
            return LinkResult.EmailMismatch();
        }
        
        // Create link
        _context.UserLogins.Add(new UserLogin
        {
            UserId = existingUser.Id,
            LoginProvider = newLogin.LoginProvider,
            ProviderKey = newLogin.ProviderKey,
            ProviderDisplayName = newLogin.ProviderDisplayName
        });
        
        // Copy claims
        var claims = newLogin.Principal.Claims
            .Where(c => !await UserHasClaim(existingUser.Id, c))
            .Select(c => new UserClaim
            {
                UserId = existingUser.Id,
                ClaimType = c.Type,
                ClaimValue = c.Value
            });
            
        _context.UserClaims.AddRange(claims);
        await _context.SaveChangesAsync();
        
        return LinkResult.Success();
    }
}
```

### Migration from Legacy System

```csharp
public class LegacyUserMigration
{
    public async Task<MigrationResult> MigrateLegacyUser(
        string email, 
        string legacyPassword)
    {
        // Check legacy system
        var legacyUser = await _legacyDb.GetUser(email);
        if (legacyUser == null)
        {
            return MigrationResult.NotFound();
        }
        
        // Verify legacy password
        if (!VerifyLegacyPassword(legacyPassword, legacyUser.PasswordHash))
        {
            return MigrationResult.InvalidPassword();
        }
        
        // Create new user
        var newUser = new ApplicationUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = legacyUser.EmailVerified,
            LegacyUserId = legacyUser.Id
        };
        
        // Create with new secure password
        var result = await _userManager.CreateAsync(newUser, legacyPassword);
        if (!result.Succeeded)
        {
            return MigrationResult.Failed(result.Errors);
        }
        
        // Migrate additional data
        await MigrateUserData(legacyUser.Id, newUser.Id);
        
        // Mark as migrated
        await _legacyDb.MarkAsMigrated(legacyUser.Id);
        
        return MigrationResult.Success(newUser);
    }
}
```

## Cost Optimization Strategies

### Scenario: 100,000 Potential Users, 2,000 Active

```yaml
Challenge:
  Total Users: 100,000
  Active Users: 2,000 annually
  Usage Pattern: Sporadic, seasonal
  Budget: Minimal
  
Strategy:
  Architecture: Hybrid approach
  Primary: Self-hosted for active users
  Fallback: JIT provisioning for dormant users
  Cost Model: Pay only for active usage
```

### Implementation Strategy

```csharp
public class CostOptimizedAuthService
{
    // Tiered user storage
    public class UserStorageStrategy
    {
        // Active users in primary database
        private readonly ApplicationDbContext _activeDb;
        
        // Dormant users in cheaper storage
        private readonly ITableStorage _dormantStorage;
        
        // Redis cache for hot users
        private readonly IDistributedCache _cache;
        
        public async Task<User> GetUser(string email)
        {
            // Check cache first (1ms)
            var cached = await _cache.GetAsync<User>($"user:{email}");
            if (cached != null) return cached;
            
            // Check active database (10ms)
            var activeUser = await _activeDb.Users
                .FirstOrDefaultAsync(u => u.Email == email);
            if (activeUser != null)
            {
                await _cache.SetAsync($"user:{email}", activeUser, 
                    TimeSpan.FromHours(1));
                return activeUser;
            }
            
            // Check dormant storage (100ms)
            var dormantUser = await _dormantStorage
                .GetEntityAsync<DormantUser>("users", email);
            if (dormantUser != null)
            {
                // Reactivate user
                return await ReactivateUser(dormantUser);
            }
            
            return null;
        }
        
        public async Task<User> ReactivateUser(DormantUser dormant)
        {
            // Move to active database
            var user = new User
            {
                Email = dormant.Email,
                PasswordHash = dormant.PasswordHash,
                CreatedAt = dormant.CreatedAt,
                LastLoginAt = DateTime.UtcNow
            };
            
            _activeDb.Users.Add(user);
            await _activeDb.SaveChangesAsync();
            
            // Remove from dormant storage
            await _dormantStorage.DeleteEntityAsync("users", dormant.Email);
            
            // Cache for immediate use
            await _cache.SetAsync($"user:{email}", user, 
                TimeSpan.FromHours(1));
                
            return user;
        }
    }
    
    // Archive inactive users
    public class UserArchivalService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // Run daily at 2 AM
                var now = DateTime.Now;
                var scheduledTime = now.Date.AddDays(1).AddHours(2);
                var delay = scheduledTime - now;
                
                await Task.Delay(delay, ct);
                
                // Archive users inactive for 90 days
                var cutoffDate = DateTime.UtcNow.AddDays(-90);
                var inactiveUsers = await _activeDb.Users
                    .Where(u => u.LastLoginAt < cutoffDate)
                    .Take(1000) // Batch process
                    .ToListAsync(ct);
                    
                foreach (var user in inactiveUsers)
                {
                    await ArchiveUser(user);
                }
            }
        }
    }
}
```

### Cost-Effective Token Strategy

```csharp
public class EconomicalTokenService
{
    // Use stateless JWTs to avoid database lookups
    public class StatelessJwtService
    {
        public string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("active", "true") // Mark as active user
            };
            
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(
                key, 
                SecurityAlgorithms.HmacSha256);
                
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1), // Short-lived
                signingCredentials: creds);
                
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        
        // Use Redis for refresh tokens (only for active users)
        public async Task<string> GenerateRefreshToken(string userId)
        {
            var token = GenerateSecureToken();
            
            await _redis.StringSetAsync(
                $"refresh:{token}",
                userId,
                TimeSpan.FromDays(30));
                
            return token;
        }
    }
}
```

### Infrastructure Cost Optimization

```yaml
# Docker Compose for self-hosted auth
version: '3.8'

services:
  auth-api:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=PostgreSQL # Cheaper than SQL Server
    deploy:
      resources:
        limits:
          memory: 512M # Minimal memory
          cpus: '0.5'
    
  postgres:
    image: postgres:alpine # Smaller image
    environment:
      - POSTGRES_DB=auth
      - POSTGRES_USER=auth
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    deploy:
      resources:
        limits:
          memory: 256M
          
  redis:
    image: redis:alpine
    command: >
      redis-server
      --maxmemory 128mb
      --maxmemory-policy allkeys-lru
    deploy:
      resources:
        limits:
          memory: 128M
          
  # Use Nginx for SSL termination and caching
  nginx:
    image: nginx:alpine
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./ssl:/etc/nginx/ssl
    ports:
      - "443:443"
    deploy:
      resources:
        limits:
          memory: 64M
```

### Provider Cost Comparison

```yaml
Scenario: 100K users, 2K active monthly

Self-Hosted:
  Infrastructure:
    - VPS: $40/month (4GB RAM, 2 vCPU)
    - Database: Included
    - SSL Cert: $10/month
    - Backup: $5/month
  Total: $55/month
  
Auth0:
  Free Tier: 7,000 active users
  B2C Essential: $1,400/month (100K users)
  Only Active: ~$30/month (2K users) if negotiated
  
AWS Cognito:
  First 50K MAU: Free
  Next 50K: $0.0055/MAU
  Our Cost: $0 (under free tier)
  
Azure AD B2C:
  First 50K MAU: Free
  Next 50K: $0.0055/MAU
  Our Cost: $0 (under free tier)
  
Recommendation: 
  - Start with AWS Cognito or Azure AD B2C
  - Self-host only if special requirements
  - Negotiate enterprise deals if growing
```

## Security Best Practices

### OAuth Security Checklist

```csharp
public class OAuthSecurityConfig
{
    public void ConfigureSecureOAuth(IServiceCollection services)
    {
        services.Configure<OAuthOptions>(options =>
        {
            // 1. Always use HTTPS
            options.RequireHttpsMetadata = true;
            
            // 2. Use PKCE for all flows
            options.UsePkce = true;
            
            // 3. Validate state parameter
            options.StateDataFormat = new PropertiesDataFormat(
                new AesDataProtector());
                
            // 4. Short-lived tokens
            options.AccessTokenLifetime = TimeSpan.FromMinutes(15);
            options.RefreshTokenLifetime = TimeSpan.FromDays(30);
            
            // 5. Secure token storage
            options.SaveTokens = false; // Don't save in cookies
            
            // 6. Strict redirect URI validation
            options.ValidateRedirectUri = true;
            options.RedirectUriValidator = new StrictRedirectUriValidator();
        });
    }
    
    // Implement secure token storage
    public class SecureTokenStorage
    {
        public async Task StoreTokens(string userId, TokenResponse tokens)
        {
            // Encrypt tokens at rest
            var encryptedAccess = _encryption.Encrypt(tokens.AccessToken);
            var encryptedRefresh = _encryption.Encrypt(tokens.RefreshToken);
            
            // Store with automatic expiration
            var pipeline = _redis.CreateBatch();
            
            pipeline.StringSetAsync(
                $"access:{userId}",
                encryptedAccess,
                TimeSpan.FromMinutes(15));
                
            pipeline.StringSetAsync(
                $"refresh:{userId}",
                encryptedRefresh,
                TimeSpan.FromDays(30));
                
            await pipeline.ExecuteAsync();
        }
    }
}
```

### Common Vulnerabilities and Mitigation

```csharp
public class SecurityMitigations
{
    // 1. Prevent token replay attacks
    public class TokenReplayPrevention
    {
        private readonly IDistributedCache _cache;
        
        public async Task<bool> ValidateNonce(string nonce)
        {
            var key = $"nonce:{nonce}";
            var exists = await _cache.GetAsync(key) != null;
            
            if (exists)
            {
                return false; // Replay attempt
            }
            
            // Store nonce for token lifetime
            await _cache.SetAsync(key, new byte[1], 
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                });
                
            return true;
        }
    }
    
    // 2. Prevent authorization code injection
    public class AuthorizationCodeValidator
    {
        public async Task<bool> ValidateCode(string code, string clientId)
        {
            var storedCode = await _cache.GetStringAsync($"authcode:{code}");
            if (storedCode == null)
            {
                return false;
            }
            
            var codeData = JsonSerializer.Deserialize<AuthCodeData>(storedCode);
            
            // Validate client
            if (codeData.ClientId != clientId)
            {
                await _logger.LogSecurityEvent("Code injection attempt", clientId);
                return false;
            }
            
            // Single use
            await _cache.RemoveAsync($"authcode:{code}");
            
            return true;
        }
    }
    
    // 3. Rate limiting
    public class AuthRateLimiter
    {
        public async Task<bool> CheckRateLimit(string identifier)
        {
            var key = $"rate:{identifier}";
            var attempts = await _cache.GetAsync<int>(key);
            
            if (attempts > 10) // 10 attempts per minute
            {
                await _logger.LogRateLimitExceeded(identifier);
                return false;
            }
            
            await _cache.IncrementAsync(key);
            await _cache.SetExpirationAsync(key, TimeSpan.FromMinutes(1));
            
            return true;
        }
    }
}
```

## Implementation Roadmap

### Phase 1: Basic Authentication (Week 1-2)

```yaml
Tasks:
  - Set up database schema
  - Implement email/password registration
  - Create login endpoints
  - Add email verification
  - Implement password reset
  - Add rate limiting
  
Deliverables:
  - Working registration/login
  - Email verification flow
  - Password reset functionality
  - Basic security measures
```

### Phase 2: OAuth Integration (Week 3-4)

```yaml
Tasks:
  - Register OAuth applications
  - Implement Google OAuth
  - Add Microsoft OAuth
  - Implement GitHub OAuth
  - Create account linking
  - Add provider selection UI
  
Deliverables:
  - Multi-provider login
  - Account linking
  - Provider management
  - Unified token system
```

### Phase 3: SSO & Enterprise (Week 5-6)

```yaml
Tasks:
  - Add SAML support
  - Implement OIDC discovery
  - Create enterprise onboarding
  - Add domain verification
  - Implement JIT provisioning
  - Create admin portal
  
Deliverables:
  - Enterprise SSO support
  - Self-service onboarding
  - Domain management
  - User provisioning
```

### Phase 4: Optimization (Week 7-8)

```yaml
Tasks:
  - Implement user archival
  - Add caching layer
  - Optimize token strategy
  - Create monitoring dashboard
  - Performance testing
  - Security audit
  
Deliverables:
  - Cost-optimized system
  - Performance metrics
  - Security report
  - Operations runbook
```

## Monitoring & Maintenance

### Key Metrics to Track

```csharp
public class AuthenticationMetrics
{
    // Real-time metrics
    public class MetricsCollector
    {
        public async Task TrackMetrics()
        {
            // Registration funnel
            _metrics.TrackGauge("auth.registrations.started", count);
            _metrics.TrackGauge("auth.registrations.completed", count);
            _metrics.TrackGauge("auth.registrations.abandoned", count);
            
            // Login metrics
            _metrics.TrackGauge("auth.logins.success", count);
            _metrics.TrackGauge("auth.logins.failed", count);
            _metrics.TrackGauge("auth.logins.locked", count);
            
            // Provider usage
            foreach (var provider in _providers)
            {
                _metrics.TrackGauge($"auth.provider.{provider}.usage", count);
                _metrics.TrackGauge($"auth.provider.{provider}.errors", count);
            }
            
            // Performance
            _metrics.TrackHistogram("auth.login.duration", duration);
            _metrics.TrackHistogram("auth.token.generation", duration);
            
            // Cost optimization
            _metrics.TrackGauge("auth.users.active", activeCount);
            _metrics.TrackGauge("auth.users.dormant", dormantCount);
            _metrics.TrackGauge("auth.storage.cost", estimatedCost);
        }
    }
}
```

### Monitoring Dashboard

```yaml
Grafana Dashboard:
  Row 1 - Overview:
    - Total users
    - Active users (24h)
    - Login success rate
    - Average login time
    
  Row 2 - Providers:
    - Provider distribution pie chart
    - Provider error rates
    - OAuth callback times
    - Token generation rate
    
  Row 3 - Security:
    - Failed login attempts
    - Locked accounts
    - Rate limit hits
    - Suspicious activity
    
  Row 4 - Performance:
    - API response times
    - Database query times
    - Cache hit rates
    - Token validation speed
    
  Row 5 - Cost:
    - Active vs dormant users
    - Storage utilization
    - Compute usage
    - Projected monthly cost
```

### Maintenance Checklist

```yaml
Daily:
  - Monitor error rates
  - Check failed login patterns
  - Review security alerts
  - Verify backup completion

Weekly:
  - Review user growth
  - Analyze provider usage
  - Check performance metrics
  - Update security rules

Monthly:
  - Rotate signing keys
  - Archive inactive users  
  - Review cost optimization
  - Update OAuth app settings
  - Security dependency updates

Quarterly:
  - Full security audit
  - Penetration testing
  - Disaster recovery drill
  - Provider agreement review
  - Cost analysis and optimization
```

## Summary & Best Practices

### Quick Decision Guide

```yaml
For 100K Users, 2K Active:
  Recommended Stack:
    - Primary: AWS Cognito or Azure AD B2C (free tier)
    - Fallback: Self-hosted for special requirements
    - Database: PostgreSQL for cost optimization
    - Caching: Redis for active users
    - Monitoring: Open source stack (Prometheus + Grafana)
    
  Architecture:
    - Stateless JWTs for scalability
    - Short-lived access tokens (15 min)
    - Refresh tokens in Redis
    - User archival after 90 days
    - JIT provisioning for returning users
    
  Security:
    - HTTPS everywhere
    - PKCE for all OAuth flows
    - Rate limiting on all endpoints
    - Regular security audits
    - Automated vulnerability scanning
```

### Do's and Don'ts

```yaml
DO:
  - Use established OAuth libraries
  - Implement account linking from start
  - Plan for provider changes
  - Monitor costs continuously
  - Archive inactive users
  - Use caching aggressively
  - Implement proper logging
  - Regular security updates

DON'T:
  - Store tokens in cookies
  - Implement OAuth from scratch
  - Trust user input
  - Ignore rate limiting
  - Keep all users in hot storage
  - Use long-lived tokens
  - Skip security headers
  - Forget about compliance
```