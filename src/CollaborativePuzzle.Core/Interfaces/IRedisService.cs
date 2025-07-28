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
        /// Gets a value from Redis (generic version)
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve</typeparam>
        /// <param name="key">Cache key</param>
        /// <returns>Value or default if not found</returns>
        Task<T?> GetAsync<T>(string key) where T : class;
        
        /// <summary>
        /// Sets a value in Redis with optional expiration
        /// </summary>
        /// <typeparam name="T">Type of value to store</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to store</param>
        /// <param name="expiry">Optional expiration time</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
        
        /// <summary>
        /// Sets a value in Redis with conditional operation
        /// </summary>
        /// <typeparam name="T">Type of value to store</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to store</param>
        /// <param name="expiry">Expiration time</param>
        /// <param name="when">Condition for the operation</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan expiry, When when) where T : class;
        
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
        
        /// <summary>
        /// Publishes an object to a Redis channel
        /// </summary>
        /// <typeparam name="T">Type of object to publish</typeparam>
        /// <param name="channel">Channel name</param>
        /// <param name="message">Object to publish</param>
        /// <returns>Task representing the operation</returns>
        Task PublishAsync<T>(string channel, T message) where T : class;

        /// <summary>
        /// Increments a value in Redis
        /// </summary>
        /// <param name="key">Key to increment</param>
        /// <param name="value">Value to increment by (default 1)</param>
        /// <returns>The value after incrementing</returns>
        Task<long> IncrementAsync(string key, long value = 1);

        /// <summary>
        /// Sets a string value in Redis
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to store</param>
        /// <param name="expiry">Optional expiration time</param>
        /// <returns>True if operation was successful</returns>
        Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null);

        /// <summary>
        /// Sets expiration time for a key
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="expiry">Expiration time</param>
        /// <returns>True if expiration was set</returns>
        Task<bool> KeyExpireAsync(string key, TimeSpan expiry);

        /// <summary>
        /// Gets all keys matching a pattern
        /// </summary>
        /// <param name="pattern">Pattern to match</param>
        /// <returns>Collection of matching keys</returns>
        Task<IEnumerable<string>> GetKeysAsync(string pattern);

        /// <summary>
        /// Gets the length of a sorted set within a score range
        /// </summary>
        /// <param name="key">Sorted set key</param>
        /// <param name="min">Minimum score</param>
        /// <param name="max">Maximum score</param>
        /// <returns>Number of elements in the range</returns>
        Task<long> SortedSetLengthAsync(string key, double min, double max);
        
        /// <summary>
        /// Gets a numeric value from Redis
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <returns>Numeric value or 0 if not found</returns>
        Task<long> GetLongAsync(string key);
    }
}