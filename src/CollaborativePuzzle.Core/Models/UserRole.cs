namespace CollaborativePuzzle.Core.Models;

public class UserRole
{
    public string UserId { get; set; } = default!;
    public string RoleId { get; set; } = default!;
    
    // Navigation properties
    public virtual User User { get; set; } = default!;
    public virtual Role Role { get; set; } = default!;
}