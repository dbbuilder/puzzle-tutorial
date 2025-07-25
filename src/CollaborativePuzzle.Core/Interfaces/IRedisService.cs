using System;
using System.Threading.Tasks;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Service interface for Redis caching operations
    /// Used as SignalR backplane and for session state caching
    /// </summary>
    public interface IRedisService
    {
        /// <summary>
        /// Sets a string value in Redis with optional expiration
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to store</param>
        /// <param name="expiry">Optional expiration time</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);
        
        /// <summary>
        /// Gets a string value from Redis
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>Cached value or null if not found</returns>
        Task<string?> GetStringAsync(string key);
        
        /// <summary>
        /// Sets an object in Redis as JSON with optional expiration
        /// </summary>
        /// <typeparam name="T">Type of object to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Object to store</param>
        /// <param name="expiry">Optional expiration time</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null);
        
        /// <summary>
        /// Gets an object from Redis and deserializes from JSON
        /// </summary>
        /// <typeparam name="T">Type of object to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Deserialized object or default if not found</returns>
        Task<T?> GetObjectAsync<T>(string key);
        
        /// <summary>
        /// Deletes a key from Redis
        /// </summary>
        /// <param name="key">Cache key to delete</param>
        /// <returns>True if key was deleted</returns>
        Task<bool> DeleteAsync(string key);
        
        /// <summary>
        /// Checks if a key exists in Redis
        /// </summary>
        /// <param name="key">Cache key to check</param>
        /// <returns>True if key exists</returns>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// Sets expiration time for an existing key
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="expiry">Expiration time</param>
        /// <returns>True if expiration was set</returns>
        Task<bool> ExpireAsync(string key, TimeSpan expiry);
        
        /// <summary>
        /// Publishes a message to a Redis channel (used for pub/sub scenarios)
        /// </summary>
        /// <param name="channel">Channel name</param>
        /// <param name="message">Message to publish</param>
        /// <returns>Number of subscribers that received the message</returns>
        Task<long> PublishAsync(string channel, string message);
    }
}
