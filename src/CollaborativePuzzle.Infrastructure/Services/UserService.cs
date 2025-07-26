using System.Security.Cryptography;
using System.Text;
using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Models;
using CollaborativePuzzle.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User = CollaborativePuzzle.Core.Models.User;

namespace CollaborativePuzzle.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(ApplicationDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuthenticationResult> ValidateCredentialsAsync(string username, string password)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    Error = "Invalid credentials"
                };
            }

            // Verify password
            if (!VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            {
                return new AuthenticationResult
                {
                    Success = false,
                    Error = "Invalid credentials"
                };
            }

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            return new AuthenticationResult
            {
                Success = true,
                User = user,
                Roles = roles
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for user: {Username}", username);
            return new AuthenticationResult
            {
                Success = false,
                Error = "An error occurred during authentication"
            };
        }
    }

    public async Task<CreateUserResult> CreateUserAsync(RegisterRequest request)
    {
        try
        {
            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return new CreateUserResult
                {
                    Success = false,
                    Error = "Username already exists"
                };
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return new CreateUserResult
                {
                    Success = false,
                    Error = "Email already exists"
                };
            }

            // Create password hash
            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);

            // Assign default role
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole != null)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = userRole.Id
                });
            }

            await _context.SaveChangesAsync();

            return new CreateUserResult
            {
                Success = true,
                User = user
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", request.Username);
            return new CreateUserResult
            {
                Success = false,
                Error = "An error occurred during user creation"
            };
        }
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Enumerable.Empty<string>();
        }

        return user.UserRoles.Select(ur => ur.Role.Name).ToList();
    }

    private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using var hmac = new HMACSHA512();
        passwordSalt = hmac.Key;
        passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    private static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
    {
        using var hmac = new HMACSHA512(storedSalt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computedHash.SequenceEqual(storedHash);
    }
}