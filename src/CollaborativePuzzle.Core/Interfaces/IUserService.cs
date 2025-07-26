using CollaborativePuzzle.Core.DTOs;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Core.Interfaces;

public interface IUserService
{
    Task<AuthenticationResult> ValidateCredentialsAsync(string username, string password);
    Task<CreateUserResult> CreateUserAsync(RegisterRequest request);
    Task<User?> GetUserByIdAsync(string userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
}

public class AuthenticationResult
{
    public bool Success { get; set; }
    public User? User { get; set; }
    public IEnumerable<string> Roles { get; set; } = new List<string>();
    public string? Error { get; set; }
}

public class CreateUserResult
{
    public bool Success { get; set; }
    public User? User { get; set; }
    public string? Error { get; set; }
}