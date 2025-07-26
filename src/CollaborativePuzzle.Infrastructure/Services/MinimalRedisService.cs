using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Enums;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CollaborativePuzzle.Infrastructure.Services
{
    /// <summary>
    /// Minimal in-memory implementation of Redis service for testing
    /// </summary>
    public class MinimalRedisService : IRedisService
    {
        private readonly ConcurrentDictionary<string, (string value, DateTime? expiry)> _cache = new();
        private readonly ConcurrentDictionary<string, List<Action<string>>> _subscribers = new();

        public Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _cache[key] = (value, expiryTime);
            return Task.FromResult(true);
        }

        public Task<string?> GetStringAsync(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (!item.expiry.HasValue || item.expiry.Value > DateTime.UtcNow)
                {
                    return Task.FromResult<string?>(item.value);
                }
                _cache.TryRemove(key, out _);
            }
            return Task.FromResult<string?>(null);
        }

        public async Task<bool> SetObjectAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(value);
            return await SetStringAsync(key, json, expiry);
        }

        public async Task<T?> GetObjectAsync<T>(string key)
        {
            var json = await GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            return GetObjectAsync<T>(key);
        }

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            return SetObjectAsync(key, value, expiry);
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiry, When when) where T : class
        {
            if (when == When.NotExists && _cache.ContainsKey(key))
                return false;
            
            if (when == When.Exists && !_cache.ContainsKey(key))
                return false;

            return await SetObjectAsync(key, value, expiry);
        }

        public Task<bool> DeleteAsync(string key)
        {
            return Task.FromResult(_cache.TryRemove(key, out _));
        }

        public Task<bool> ExistsAsync(string key)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                if (!item.expiry.HasValue || item.expiry.Value > DateTime.UtcNow)
                {
                    return Task.FromResult(true);
                }
                _cache.TryRemove(key, out _);
            }
            return Task.FromResult(false);
        }

        public Task<bool> ExpireAsync(string key, TimeSpan expiry)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                _cache[key] = (item.value, DateTime.UtcNow.Add(expiry));
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<long> PublishAsync(string channel, string message)
        {
            if (_subscribers.TryGetValue(channel, out var subscribers))
            {
                foreach (var subscriber in subscribers)
                {
                    Task.Run(() => subscriber(message));
                }
                return Task.FromResult((long)subscribers.Count);
            }
            return Task.FromResult(0L);
        }

        public async Task PublishAsync<T>(string channel, T message) where T : class
        {
            var json = JsonSerializer.Serialize(message);
            await PublishAsync(channel, json);
        }
    }
}