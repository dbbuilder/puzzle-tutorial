# MQTT Implementation Guide

## Overview
MQTT (Message Queuing Telemetry Transport) is a lightweight messaging protocol designed for IoT devices and low-bandwidth, high-latency networks. In our puzzle platform, MQTT enables integration with physical puzzle devices and IoT sensors.

## Purpose in Our Application
- **Physical puzzle controllers** for tactile interaction
- **IoT sensors** for environmental puzzle rooms
- **LED status indicators** showing puzzle progress
- **Smart home integration** for ambient puzzle experiences
- **Mobile device sensors** for gesture-based controls

## Architecture Implementation

### 1. MQTT Broker Setup
We use Azure IoT Hub as our MQTT broker for enterprise-grade reliability.

**Configuration in appsettings.json**:
```json
{
  "MQTT": {
    "BrokerHost": "your-iothub.azure-devices.net",
    "Port": 8883,
    "ClientId": "PuzzlePlatform",
    "Username": "your-iothub.azure-devices.net/PuzzlePlatform/?api-version=2021-04-12",
    "UseSsl": true,
    "Topics": {
      "PieceMovement": "puzzle/piece/movement",
      "SessionStatus": "puzzle/session/status",
      "DeviceStatus": "puzzle/device/status",
      "EnvironmentalData": "puzzle/environment/data"
    }
  }
}
```

### 2. MQTT Service Interface
**File**: `src/CollaborativePuzzle.Core/Interfaces/IMqttService.cs`

```csharp
public interface IMqttService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task PublishAsync(string topic, string payload, bool retain = false);
    Task SubscribeAsync(string topic);
    Task UnsubscribeAsync(string topic);
    event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;
}
```

### 3. MQTT Service Implementation
**File**: `src/CollaborativePuzzle.Infrastructure/Services/MqttService.cs`

```csharp
public class MqttService : IMqttService, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<MqttService> _logger;
    private readonly MqttConfiguration _config;
    
    public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;
    
    public MqttService(
        ILogger<MqttService> logger,
        IOptions<MqttConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        _mqttClient.ConnectedAsync += OnConnected;
        _mqttClient.DisconnectedAsync += OnDisconnected;
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.BrokerHost, _config.Port)
                .WithCredentials(_config.Username, _config.Password)
                .WithClientId(_config.ClientId)
                .WithTls()
                .WithCleanSession()
                .Build();
            
            await _mqttClient.ConnectAsync(options);
            _logger.LogInformation("Connected to MQTT broker");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            throw;
        }
    }
}
```

## Device Integration Scenarios

### 1. Physical Puzzle Controller
A physical device with buttons and knobs for puzzle interaction:

**Device Message Format**:
```json
{
  "deviceId": "puzzle-controller-001",
  "timestamp": "2024-01-15T10:30:00Z",
  "action": "piece_movement",
  "data": {
    "pieceId": "piece-123",
    "x": 450,
    "y": 320,
    "rotation": 90,
    "pressure": 0.8
  }
}
```

**Handler Implementation**:
```csharp
public class PhysicalControllerHandler
{
    public async Task HandlePieceMovementAsync(DeviceMessage message)
    {
        // Validate device authorization
        if (!await _deviceService.IsAuthorizedAsync(message.DeviceId))
            return;
        
        // Convert physical coordinates to virtual coordinates
        var virtualCoords = _coordinateMapper.PhysicalToVirtual(
            message.Data.X, message.Data.Y);
        
        // Send to puzzle session via SignalR
        await _puzzleHub.Clients.Group(message.SessionId)
            .SendAsync("PieceMovedByDevice", new
            {
                PieceId = message.Data.PieceId,
                X = virtualCoords.X,
                Y = virtualCoords.Y,
                Rotation = message.Data.Rotation,
                DeviceId = message.DeviceId
            });
    }
}
```

### 2. Environmental Sensors
Sensors that provide context for puzzle rooms:

**Sensor Data Topics**:
```
puzzle/environment/temperature  -> Room temperature
puzzle/environment/humidity     -> Room humidity  
puzzle/environment/lighting     -> Light levels
puzzle/environment/occupancy    -> People count
puzzle/environment/noise        -> Ambient noise level
```

**Environmental Handler**:
```csharp
public class EnvironmentalDataHandler
{
    public async Task HandleEnvironmentalDataAsync(string topic, string payload)
    {
        var data = JsonSerializer.Deserialize<EnvironmentalData>(payload);
        
        switch (topic)
        {
            case "puzzle/environment/lighting":
                await AdjustPuzzleDisplayBrightness(data.Value);
                break;
                
            case "puzzle/environment/occupancy":
                await UpdateSessionCapacity(data.RoomId, data.Count);
                break;
                
            case "puzzle/environment/noise":
                await AdjustVoiceChatSensitivity(data.Value);
                break;
        }
    }
}
```

### 3. LED Status Indicators
Smart LEDs that reflect puzzle progress:

**Status Topics**:
```
puzzle/led/progress/{sessionId}    -> Overall progress (0-100%)
puzzle/led/activity/{sessionId}    -> User activity indicator
puzzle/led/completion/{sessionId}  -> Completion celebration
```

**LED Controller**:
```csharp
public class LedIndicatorService
{
    public async Task UpdateProgressLEDs(string sessionId, int completionPercentage)
    {
        var message = new
        {
            SessionId = sessionId,
            Progress = completionPercentage,
            Color = GetProgressColor(completionPercentage),
            Brightness = CalculateBrightness(completionPercentage)
        };
        
        await _mqttService.PublishAsync(
            $"puzzle/led/progress/{sessionId}",
            JsonSerializer.Serialize(message)
        );
    }
    
    private string GetProgressColor(int percentage)
    {
        return percentage switch
        {
            < 25 => "#FF0000",  // Red
            < 50 => "#FFA500",  // Orange  
            < 75 => "#FFFF00",  // Yellow
            < 100 => "#00FF00", // Green
            100 => "#0000FF"    // Blue (celebration)
        };
    }
}
```

## Message Routing and Processing

### 1. Topic-Based Routing
```csharp
public class MqttMessageRouter
{
    private readonly Dictionary<string, Func<string, Task>> _handlers;
    
    public MqttMessageRouter()
    {
        _handlers = new Dictionary<string, Func<string, Task>>
        {
            ["puzzle/piece/+"] = HandlePieceMessage,
            ["puzzle/session/+"] = HandleSessionMessage,
            ["puzzle/device/+"] = HandleDeviceMessage,
            ["puzzle/environment/+"] = HandleEnvironmentalMessage
        };
    }
    
    public async Task RouteMessageAsync(string topic, string payload)
    {
        foreach (var (pattern, handler) in _handlers)
        {
            if (TopicMatches(topic, pattern))
            {
                await handler(payload);
                break;
            }
        }
    }
}
```

### 2. Message Validation and Security
```csharp
public class MqttMessageValidator
{
    public bool ValidateMessage(string topic, string payload)
    {
        // Validate topic format
        if (!IsValidTopicFormat(topic))
            return false;
        
        // Validate payload structure
        if (!IsValidPayloadStructure(payload))
            return false;
        
        // Check message size limits
        if (payload.Length > _maxMessageSize)
            return false;
        
        return true;
    }
    
    public async Task<bool> AuthorizeDeviceAsync(string deviceId, string topic)
    {
        // Check device permissions for specific topics
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        return device?.HasPermission(topic) ?? false;
    }
}
```

## Quality of Service (QoS) Management

### 1. QoS Level Selection
```csharp
public class MqttQosManager
{
    public MqttQualityOfServiceLevel GetQosForTopic(string topic)
    {
        return topic switch
        {
            // Critical piece movements - At least once delivery
            var t when t.StartsWith("puzzle/piece/") => MqttQualityOfServiceLevel.AtLeastOnce,
            
            // Session status - Exactly once delivery
            var t when t.StartsWith("puzzle/session/") => MqttQualityOfServiceLevel.ExactlyOnce,
            
            // Environmental data - At most once (fire and forget)
            var t when t.StartsWith("puzzle/environment/") => MqttQualityOfServiceLevel.AtMostOnce,
            
            // Default to at least once
            _ => MqttQualityOfServiceLevel.AtLeastOnce
        };
    }
}
```

### 2. Retained Messages
```csharp
public async Task PublishSessionStatusAsync(string sessionId, SessionStatus status)
{
    var message = new
    {
        SessionId = sessionId,
        Status = status.ToString(),
        Timestamp = DateTimeOffset.UtcNow,
        ParticipantCount = await GetParticipantCountAsync(sessionId)
    };
    
    // Retain session status so new devices get current state
    await _mqttService.PublishAsync(
        $"puzzle/session/status/{sessionId}",
        JsonSerializer.Serialize(message),
        retain: true
    );
}
```

## Device Management

### 1. Device Registration
```csharp
public class IoTDeviceManager
{
    public async Task<DeviceRegistrationResult> RegisterDeviceAsync(DeviceRegistrationRequest request)
    {
        // Validate device credentials
        if (!await ValidateDeviceCredentialsAsync(request))
            return DeviceRegistrationResult.InvalidCredentials();
        
        // Create device entry
        var device = new IoTDevice
        {
            Id = request.DeviceId,
            Type = request.DeviceType,
            Location = request.Location,
            Capabilities = request.Capabilities,
            LastSeen = DateTimeOffset.UtcNow,
            IsActive = true
        };
        
        await _deviceRepository.CreateAsync(device);
        
        // Generate MQTT credentials
        var mqttCredentials = await _mqttCredentialService.GenerateAsync(device.Id);
        
        return DeviceRegistrationResult.Success(mqttCredentials);
    }
}
```

### 2. Device Health Monitoring
```csharp
public class DeviceHealthMonitor
{
    public async Task MonitorDeviceHealthAsync()
    {
        var devices = await _deviceRepository.GetActiveDevicesAsync();
        
        foreach (var device in devices)
        {
            var lastSeen = device.LastHeartbeat;
            var threshold = TimeSpan.FromMinutes(5);
            
            if (DateTimeOffset.UtcNow - lastSeen > threshold)
            {
                // Mark device as offline
                await MarkDeviceOfflineAsync(device.Id);
                
                // Publish device status
                await _mqttService.PublishAsync(
                    $"puzzle/device/status/{device.Id}",
                    JsonSerializer.Serialize(new { Status = "offline" }),
                    retain: true
                );
                
                // Clean up any locked pieces by this device
                await _pieceRepository.UnlockAllPiecesByDeviceAsync(device.Id);
            }
        }
    }
}
```

## Integration with Web Platform

### 1. MQTT-to-SignalR Bridge
```csharp
public class MqttSignalRBridge
{
    public async Task BridgeMessageAsync(string topic, string payload)
    {
        // Convert MQTT message to SignalR format
        var signalRMessage = ConvertMqttToSignalR(topic, payload);
        
        // Determine target SignalR group based on topic
        var groupName = ExtractGroupFromTopic(topic);
        
        // Send to appropriate SignalR clients
        await _hubContext.Clients.Group(groupName)
            .SendAsync("MqttMessage", signalRMessage);
    }
    
    private string ExtractGroupFromTopic(string topic)
    {
        // Extract session ID from topic like "puzzle/session/abc123/status"
        var parts = topic.Split('/');
        if (parts.Length >= 3 && parts[1] == "session")
        {
            return $"session_{parts[2]}";
        }
        
        return "all";
    }
}
```

### 2. Web-to-MQTT Commands
```csharp
public class WebMqttCommandService
{
    public async Task SendDeviceCommandAsync(string deviceId, DeviceCommand command)
    {
        var topic = $"puzzle/device/command/{deviceId}";
        var payload = JsonSerializer.Serialize(command);
        
        await _mqttService.PublishAsync(topic, payload);
        
        // Log command for audit trail
        _logger.LogInformation("Sent command {Command} to device {DeviceId}", 
            command.Type, deviceId);
    }
}
```

## Security Implementation

### 1. Device Authentication
```csharp
public class MqttDeviceAuthenticator
{
    public async Task<bool> AuthenticateDeviceAsync(string clientId, string username, string password)
    {
        // Validate client certificate if using mutual TLS
        if (_config.UseMutualTls)
        {
            var certificate = GetClientCertificate();
            if (!ValidateCertificate(certificate))
                return false;
        }
        
        // Validate device credentials against database
        var device = await _deviceRepository.GetByIdAsync(clientId);
        if (device == null || !device.IsActive)
            return false;
        
        // Verify password hash
        return _passwordHasher.VerifyHashedPassword(device, device.PasswordHash, password) 
            != PasswordVerificationResult.Failed;
    }
}
```

### 2. Topic Authorization
```csharp
public class MqttTopicAuthorizer
{
    public async Task<bool> AuthorizeTopicAsync(string clientId, string topic, MqttAccessType accessType)
    {
        var device = await _deviceRepository.GetByIdAsync(clientId);
        if (device == null)
            return false;
        
        // Check device permissions for topic pattern
        var permissions = device.TopicPermissions;
        
        foreach (var permission in permissions)
        {
            if (TopicMatches(topic, permission.TopicPattern) && 
                permission.AccessType.HasFlag(accessType))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

## Performance Optimization

### 1. Connection Pool Management
```csharp
public class MqttConnectionPool
{
    private readonly ConcurrentQueue<IMqttClient> _availableConnections;
    private readonly SemaphoreSlim _connectionSemaphore;
    
    public async Task<IMqttClient> GetConnectionAsync()
    {
        await _connectionSemaphore.WaitAsync();
        
        if (_availableConnections.TryDequeue(out var connection) && 
            connection.IsConnected)
        {
            return connection;
        }
        
        // Create new connection if none available
        return await CreateNewConnectionAsync();
    }
    
    public void ReturnConnection(IMqttClient connection)
    {
        if (connection.IsConnected)
        {
            _availableConnections.Enqueue(connection);
        }
        
        _connectionSemaphore.Release();
    }
}
```

### 2. Message Batching
```csharp
public class MqttMessageBatcher
{
    private readonly List<MqttMessage> _messageBuffer = new();
    private readonly Timer _flushTimer;
    
    public void AddMessage(string topic, string payload)
    {
        lock (_messageBuffer)
        {
            _messageBuffer.Add(new MqttMessage(topic, payload));
            
            if (_messageBuffer.Count >= _batchSize)
            {
                _ = Task.Run(FlushBatchAsync);
            }
        }
    }
    
    private async Task FlushBatchAsync()
    {
        List<MqttMessage> messagesToSend;
        
        lock (_messageBuffer)
        {
            messagesToSend = new List<MqttMessage>(_messageBuffer);
            _messageBuffer.Clear();
        }
        
        foreach (var message in messagesToSend)
        {
            await _mqttService.PublishAsync(message.Topic, message.Payload);
        }
    }
}
```

This MQTT implementation provides robust IoT device integration capabilities, enabling physical puzzle controllers, environmental sensors, and smart indicators to seamlessly integrate with the collaborative puzzle platform.
