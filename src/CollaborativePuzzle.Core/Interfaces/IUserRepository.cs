using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CollaborativePuzzle.Core.Entities;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Repository interface for user data access operations
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Gets a user by ID
        /// </summary>
        Task<User?> GetUserAsync(Guid userId);
        
        /// <summary>
        /// Gets a user by username
        /// </summary>
        Task<User?> GetUserByUsernameAsync(string username);
        
        /// <summary>
        /// Gets a user by email
        /// </summary>
        Task<User?> GetUserByEmailAsync(string email);
        
        /// <summary>
        /// Creates a new user
        /// </summary>
        Task<User> CreateUserAsync(User user);
        
        /// <summary>
        /// Updates a user
        /// </summary>
        Task<bool> UpdateUserAsync(User user);
        
        /// <summary>
        /// Deletes a user
        /// </summary>
        Task<bool> DeleteUserAsync(Guid userId);
        
        /// <summary>
        /// Updates user profile information
        /// </summary>
        Task<bool> UpdateUserProfileAsync(Guid userId, string displayName, string? avatarUrl);
        
        /// <summary>
        /// Updates user statistics
        /// </summary>
        Task<bool> UpdateUserStatsAsync(Guid userId, UserStats stats);
        
        /// <summary>
        /// Gets user statistics
        /// </summary>
        Task<UserStats?> GetUserStatsAsync(Guid userId);
        
        /// <summary>
        /// Gets recently active users
        /// </summary>
        Task<IEnumerable<User>> GetActiveUsersAsync(int limit = 100);
        
        /// <summary>
        /// Updates user's last active timestamp
        /// </summary>
        Task<bool> UpdateLastActiveAsync(Guid userId);
        
        /// <summary>
        /// Authenticates a user
        /// </summary>
        Task<User?> AuthenticateAsync(string username, string passwordHash);
        
        /// <summary>
        /// Updates user password
        /// </summary>
        Task<bool> UpdatePasswordAsync(Guid userId, string newPasswordHash);
    }
}