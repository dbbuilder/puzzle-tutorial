using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollaborativePuzzle.Core.Interfaces;

namespace CollaborativePuzzle.Infrastructure.Services
{
    /// <summary>
    /// Redis service implementation using StackExchange.Redis
    /// Provides caching, session state, and SignalR backplane functionality
    /// </summary>
    public class RedisService : IRedisService, IDisposable
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _database;
        private readonly ISubscriber _subscriber;
        private readonly ILogger<RedisService> _logger;
        private readonly RedisConfiguration _config;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        public RedisService(
            IConnectionMultiplexer connectionMultiplexer,
            ILogger<RedisService> logger,
            IOptions<RedisConfiguration> config)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config.Value ?? throw new ArgumentNullException(nameof(config));

            _database = _connectionMultiplexer.GetDatabase(_config.Database);
            _subscriber = _connectionMultiplexer.GetSubscriber();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            _logger.LogInformation("Redis service initialized with database {Database}", _config.Database);
        }

        public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var fullKey = BuildKey(key);
                var expiryToUse = expiry ?? _config.DefaultExpiry;

                var result = await _database.StringSetAsync(fullKey, value, expiryToUse);

                if (result)
                {
                    _logger.LogDebug("Set Redis string key {Key} with expiry {Expiry}", fullKey, expiryToUse);
                }
                else
                {
                    _logger.LogWarning("Failed to set Redis string key {Key}", fullKey);
                }

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error setting string key {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Redis string key {Key}", key);
                throw;
            }
        }

        public async Task<string?> GetStringAsync(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var fullKey = BuildKey(key);
                var value = await _database.StringGetAsync(fullKey);

                if (value.HasValue)
                {
                    _logger.LogDebug("Retrieved Redis string key {Key}", fullKey);
                    return value;
                }

                _logger.LogDebug("Redis string key {Key} not found", fullKey);
                return null;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting string key {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis string key {Key}", key);
                throw;
            }
        }

        public async Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var result = await SetStringAsync(key, json, expiry);

                if (result)
                {
                    _logger.LogDebug("Set Redis object key {Key} of type {Type}", key, typeof(T).Name);
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error setting object key {Key} of type {Type}", key, typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Redis object key {Key} of type {Type}", key, typeof(T).Name);
                throw;
            }
        }

        public async Task<T?> GetObjectAsync<T>(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var json = await GetStringAsync(key);
                if (string.IsNullOrEmpty(json))
                {
                    _logger.LogDebug("Redis object key {Key} not found", key);
                    return default(T);
                }

                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                _logger.LogDebug("Retrieved and deserialized Redis object key {Key} of type {Type}", key, typeof(T).Name);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error getting object key {Key} of type {Type}", key, typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis object key {Key} of type {Type}", key, typeof(T).Name);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var fullKey = BuildKey(key);
                var result = await _database.KeyDeleteAsync(fullKey);

                if (result)
                {
                    _logger.LogDebug("Deleted Redis key {Key}", fullKey);
                }
                else
                {
                    _logger.LogDebug("Redis key {Key} not found for deletion", fullKey);
                }

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error deleting key {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Redis key {Key}", key);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var fullKey = BuildKey(key);
                var result = await _database.KeyExistsAsync(fullKey);

                _logger.LogDebug("Redis key {Key} exists: {Exists}", fullKey, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error checking existence of key {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of Redis key {Key}", key);
                throw;
            }
        }

        public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var fullKey = BuildKey(key);
                var result = await _database.KeyExpireAsync(fullKey, expiry);

                if (result)
                {
                    _logger.LogDebug("Set expiration for Redis key {Key} to {Expiry}", fullKey, expiry);
                }
                else
                {
                    _logger.LogWarning("Failed to set expiration for Redis key {Key}", fullKey);
                }

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error setting expiration for key {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting expiration for Redis key {Key}", key);
                throw;
            }
        }

        public async Task<long> PublishAsync(string channel, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(channel))
                {
                    throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
                }

                if (string.IsNullOrEmpty(message))
                {
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));
                }

                var subscriberCount = await _subscriber.PublishAsync(channel, message);

                _logger.LogDebug("Published message to Redis channel {Channel}, {SubscriberCount} subscribers notified", 
                    channel, subscriberCount);

                return subscriberCount;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error publishing to channel {Channel}", channel);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to Redis channel {Channel}", channel);
                throw;
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            return await GetObjectAsync<T>(key);
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            return await SetObjectAsync(key, value, expiry);
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiry, CollaborativePuzzle.Core.Interfaces.When when) where T : class
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var fullKey = BuildKey(key);
            
            var redisWhen = when switch
            {
                CollaborativePuzzle.Core.Interfaces.When.Always => StackExchange.Redis.When.Always,
                CollaborativePuzzle.Core.Interfaces.When.Exists => StackExchange.Redis.When.Exists,
                CollaborativePuzzle.Core.Interfaces.When.NotExists => StackExchange.Redis.When.NotExists,
                _ => StackExchange.Redis.When.Always
            };
            
            return await _database.StringSetAsync(fullKey, json, expiry, redisWhen);
        }

        public async Task PublishAsync<T>(string channel, T message) where T : class
        {
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            await PublishAsync(channel, json);
        }

        #region Additional Redis Operations for Advanced Features

        /// <summary>
        /// Add value to a Redis set
        /// </summary>
        public async Task<bool> SetAddAsync(string key, string value)
        {
            try
            {
                var fullKey = BuildKey(key);
                var result = await _database.SetAddAsync(fullKey, value);

                _logger.LogDebug("Added value to Redis set {Key}: {Added}", fullKey, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error adding to set {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to Redis set {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Remove value from a Redis set
        /// </summary>
        public async Task<bool> SetRemoveAsync(string key, string value)
        {
            try
            {
                var fullKey = BuildKey(key);
                var result = await _database.SetRemoveAsync(fullKey, value);

                _logger.LogDebug("Removed value from Redis set {Key}: {Removed}", fullKey, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error removing from set {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from Redis set {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Get all members of a Redis set
        /// </summary>
        public async Task<IEnumerable<string>> SetMembersAsync(string key)
        {
            try
            {
                var fullKey = BuildKey(key);
                var members = await _database.SetMembersAsync(fullKey);

                var result = members.Select(m => m.ToString()).Where(s => !string.IsNullOrEmpty(s));
                _logger.LogDebug("Retrieved {Count} members from Redis set {Key}", result.Count(), fullKey);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting set members {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis set members {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Execute a Lua script on Redis
        /// </summary>
        public async Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[] keys, RedisValue[] values)
        {
            try
            {
                var result = await _database.ScriptEvaluateAsync(script, keys, values);

                _logger.LogDebug("Executed Redis Lua script with {KeyCount} keys and {ValueCount} values", 
                    keys.Length, values.Length);

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error executing Lua script");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Redis Lua script");
                throw;
            }
        }

        /// <summary>
        /// Get multiple keys in a single operation
        /// </summary>
        public async Task<IDictionary<string, string?>> GetMultipleAsync(IEnumerable<string> keys)
        {
            try
            {
                var keysList = keys.ToList();
                var redisKeys = keysList.Select(k => (RedisKey)BuildKey(k)).ToArray();

                var values = await _database.StringGetAsync(redisKeys);

                var result = new Dictionary<string, string?>();
                for (int i = 0; i < keysList.Count; i++)
                {
                    result[keysList[i]] = values[i].HasValue ? values[i].ToString() : null;
                }

                _logger.LogDebug("Retrieved {Count} Redis keys in batch operation", keysList.Count);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting multiple keys");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multiple Redis keys");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Build a prefixed key for Redis to avoid collisions
        /// </summary>
        private string BuildKey(string key)
        {
            if (string.IsNullOrEmpty(_config.InstanceName))
            {
                return key;
            }

            return $"{_config.InstanceName}:{key}";
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Connection multiplexer is managed by DI container, don't dispose it here
                _logger.LogDebug("Redis service disposed");
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration class for Redis settings
    /// </summary>
    public class RedisConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string InstanceName { get; set; } = "PuzzlePlatform";
        public int Database { get; set; }
        public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromHours(1);
        public bool EnableKeyspaceNotifications { get; set; } = true;
        public int CommandTimeout { get; set; } = 5000;
        public int ConnectTimeout { get; set; } = 5000;
        public bool AbortOnConnectFail { get; set; }
        public int ConnectRetry { get; set; } = 3;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;
    }
}
