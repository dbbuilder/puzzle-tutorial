using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CollaborativePuzzle.IntegrationTests.Infrastructure;

public class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string DefaultUserId { get; set; } = "test-user-123";
    public string DefaultUserName { get; set; } = "Test User";
    public string DefaultUserEmail { get; set; } = "test@example.com";
    public string[] DefaultRoles { get; set; } = new[] { "User" };
}

public class TestAuthenticationHandler : AuthenticationHandler<TestAuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create test claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Options.DefaultUserId),
            new Claim(ClaimTypes.Name, Options.DefaultUserName),
            new Claim(ClaimTypes.Email, Options.DefaultUserEmail)
        };

        // Add roles
        foreach (var role in Options.DefaultRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}