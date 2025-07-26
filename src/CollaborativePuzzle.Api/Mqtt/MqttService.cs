using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Text;
using System.Text.Json;

namespace CollaborativePuzzle.Api.Mqtt
{
    public interface IMqttService
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task PublishAsync<T>(string topic, T payload, bool retain = false);
        Task SubscribeAsync(string topic, Func<string, string, Task> messageHandler);
        Task UnsubscribeAsync(string topic);
        bool IsConnected { get; }
    }

    public class MqttService : IMqttService, IDisposable
    {
        private readonly ILogger<MqttService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IManagedMqttClient _mqttClient;
        private readonly Dictionary<string, Func<string, string, Task>> _messageHandlers;
        private bool _disposed;

        public bool IsConnected => _mqttClient?.IsConnected ?? false;

        public MqttService(ILogger<MqttService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _messageHandlers = new Dictionary<string, Func<string, string, Task>>();

            var factory = new MqttFactory();
            _mqttClient = factory.CreateManagedMqttClient();

            // Setup message handling
            _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
            _mqttClient.ConnectedAsync += HandleConnectedAsync;
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            _mqttClient.ConnectingFailedAsync += HandleConnectingFailedAsync;
        }

        public async Task ConnectAsync()
        {
            try
            {
                var brokerHost = _configuration["Mqtt:BrokerHost"] ?? "localhost";
                var brokerPort = int.Parse(_configuration["Mqtt:BrokerPort"] ?? "1883");
                var clientId = _configuration["Mqtt:ClientId"] ?? $"puzzle-api-{Guid.NewGuid():N}";
                var username = _configuration["Mqtt:Username"];
                var password = _configuration["Mqtt:Password"];

                var options = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithTcpServer(brokerHost, brokerPort)
                        .WithClientId(clientId)
                        .WithCredentials(username, password)
                        .WithCleanSession()
                        .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                        .Build())
                    .Build();

                await _mqttClient.StartAsync(options);
                _logger.LogInformation("MQTT client connecting to {Host}:{Port}", brokerHost, brokerPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MQTT broker");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.StopAsync();
                    _logger.LogInformation("MQTT client disconnected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from MQTT broker");
            }
        }

        public async Task PublishAsync<T>(string topic, T payload, bool retain = false)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(retain)
                    .Build();

                await _mqttClient.EnqueueAsync(message);
                _logger.LogDebug("Published message to topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to topic {Topic}", topic);
                throw;
            }
        }

        public async Task SubscribeAsync(string topic, Func<string, string, Task> messageHandler)
        {
            try
            {
                _messageHandlers[topic] = messageHandler;

                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient.SubscribeAsync(new[] { topicFilter });
                _logger.LogInformation("Subscribed to topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic {Topic}", topic);
                throw;
            }
        }

        public async Task UnsubscribeAsync(string topic)
        {
            try
            {
                _messageHandlers.Remove(topic);
                await _mqttClient.UnsubscribeAsync(new[] { topic });
                _logger.LogInformation("Unsubscribed from topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from topic {Topic}", topic);
            }
        }

        private async Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                
                _logger.LogDebug("Received message on topic {Topic}: {Payload}", topic, payload);

                // Find matching handlers (considering wildcards)
                var matchingHandlers = _messageHandlers
                    .Where(h => IsTopicMatch(h.Key, topic))
                    .Select(h => h.Value);

                foreach (var handler in matchingHandlers)
                {
                    try
                    {
                        await handler(topic, payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in message handler for topic {Topic}", topic);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message");
            }
        }

        private bool IsTopicMatch(string pattern, string topic)
        {
            // Simple wildcard matching
            if (pattern == topic) return true;
            if (pattern.EndsWith("#"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return topic.StartsWith(prefix);
            }
            if (pattern.Contains("+"))
            {
                var parts = pattern.Split('/');
                var topicParts = topic.Split('/');
                if (parts.Length != topicParts.Length) return false;
                
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != "+" && parts[i] != topicParts[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        private Task HandleConnectedAsync(EventArgs e)
        {
            _logger.LogInformation("MQTT client connected");
            return Task.CompletedTask;
        }

        private Task HandleDisconnectedAsync(EventArgs e)
        {
            _logger.LogWarning("MQTT client disconnected");
            return Task.CompletedTask;
        }

        private Task HandleConnectingFailedAsync(ConnectingFailedEventArgs e)
        {
            _logger.LogError(e.Exception, "MQTT connection failed");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _mqttClient?.Dispose();
            _disposed = true;
        }
    }
}