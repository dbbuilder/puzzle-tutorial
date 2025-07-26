using System.ComponentModel.DataAnnotations;

namespace CollaborativePuzzle.Core.DTOs;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = default!;
    
    [Required]
    public string Password { get; set; } = default!;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
}

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    public string Username { get; set; } = default!;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;
    
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = default!;
    
    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = default!;
}

public class RegisterResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
}

public class RefreshTokenRequest
{
    [Required]
    public string Token { get; set; } = default!;
}

public class RefreshTokenResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Error { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public IEnumerable<string> Roles { get; set; } = new List<string>();
}