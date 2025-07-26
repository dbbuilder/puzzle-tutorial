using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(ILogger<UserRepository> logger)
        {
            _logger = logger;
        }

        public Task<User?> GetUserAsync(Guid userId)
        {
            _logger.LogDebug("GetUserAsync called for {UserId}", userId);
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetUserByUsernameAsync(string username)
        {
            _logger.LogDebug("GetUserByUsernameAsync called for {Username}", username);
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetUserByEmailAsync(string email)
        {
            _logger.LogDebug("GetUserByEmailAsync called for {Email}", email);
            return Task.FromResult<User?>(null);
        }

        public Task<User> CreateUserAsync(string username, string email, string passwordHash)
        {
            throw new NotImplementedException("CreateUserAsync not yet implemented");
        }

        public Task<bool> UpdateUserAsync(User user)
        {
            _logger.LogDebug("UpdateUserAsync called for user {UserId}", user.Id);
            return Task.FromResult(false);
        }

        public Task<bool> UpdateLastActiveAsync(Guid userId)
        {
            _logger.LogDebug("UpdateLastActiveAsync called for {UserId}", userId);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<User>> GetActiveUsersAsync(int minutes = 30, int limit = 50)
        {
            _logger.LogDebug("GetActiveUsersAsync called with minutes {Minutes}, limit {Limit}", minutes, limit);
            return Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
        }
    }
}