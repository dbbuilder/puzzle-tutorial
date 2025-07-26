using Microsoft.AspNetCore.SignalR;
using CollaborativePuzzle.Hubs;
using System.Text.Json;

namespace CollaborativePuzzle.Api.Mqtt
{
    /// <summary>
    /// Processes MQTT messages and bridges them to SignalR for real-time UI updates
    /// </summary>
    public class MqttMessageProcessor : IHostedService
    {
        private readonly IMqttService _mqttService;
        private readonly IHubContext<TestPuzzleHub> _hubContext;
        private readonly ILogger<MqttMessageProcessor> _logger;
        private readonly IServiceProvider _serviceProvider;

        public MqttMessageProcessor(
            IMqttService mqttService,
            IHubContext<TestPuzzleHub> hubContext,
            ILogger<MqttMessageProcessor> logger,
            IServiceProvider serviceProvider)
        {
            _mqttService = mqttService;
            _hubContext = hubContext;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting MQTT message processor");

            // Subscribe to all puzzle-related topics
            await _mqttService.SubscribeAsync("puzzle/+/+", ProcessPuzzleMessage);
            await _mqttService.SubscribeAsync("puzzle/table/+/sensors", ProcessTableSensorData);
            await _mqttService.SubscribeAsync("puzzle/environment/+", ProcessEnvironmentalData);
            await _mqttService.SubscribeAsync("puzzle/player/+/biometrics", ProcessBiometricData);
            await _mqttService.SubscribeAsync("puzzle/controller/+/input", ProcessControllerInput);
            await _mqttService.SubscribeAsync("puzzle/storage/+/status", ProcessStorageStatus);

            // Subscribe to system topics
            await _mqttService.SubscribeAsync("$SYS/broker/clients/+", ProcessSystemMessage);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping MQTT message processor");
            
            // Unsubscribe from all topics
            await _mqttService.UnsubscribeAsync("puzzle/+/+");
            await _mqttService.UnsubscribeAsync("puzzle/table/+/sensors");
            await _mqttService.UnsubscribeAsync("puzzle/environment/+");
            await _mqttService.UnsubscribeAsync("puzzle/player/+/biometrics");
            await _mqttService.UnsubscribeAsync("puzzle/controller/+/input");
            await _mqttService.UnsubscribeAsync("puzzle/storage/+/status");
            await _mqttService.UnsubscribeAsync("$SYS/broker/clients/+");
        }

        private async Task ProcessPuzzleMessage(string topic, string payload)
        {
            try
            {
                _logger.LogDebug("Processing puzzle message from topic: {Topic}", topic);
                
                // Broadcast raw MQTT data to all clients
                await _hubContext.Clients.All.SendAsync("MqttMessage", new
                {
                    topic,
                    payload,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing puzzle message");
            }
        }

        private async Task ProcessTableSensorData(string topic, string payload)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                var tableId = ExtractIdFromTopic(topic, 2);

                // Process pressure data for piece detection
                if (data.TryGetProperty("sensors", out var sensors))
                {
                    if (sensors.TryGetProperty("pressure", out var pressure))
                    {
                        var activePieces = pressure.GetProperty("activePieces").GetInt32();
                        
                        // Notify clients about piece activity
                        await _hubContext.Clients.All.SendAsync("TableSensorUpdate", new
                        {
                            tableId,
                            type = "pressure",
                            activePieces,
                            timestamp = DateTime.UtcNow
                        });
                    }

                    // Process RFID data for piece tracking
                    if (sensors.TryGetProperty("rfid", out var rfid))
                    {
                        var pieces = rfid.GetProperty("detectedPieces");
                        
                        await _hubContext.Clients.All.SendAsync("PieceDetected", new
                        {
                            tableId,
                            type = "rfid",
                            pieces = pieces.ToString(),
                            timestamp = DateTime.UtcNow
                        });
                    }

                    // Process touch gestures
                    if (sensors.TryGetProperty("touch", out var touch))
                    {
                        var gesture = touch.GetProperty("gestures").GetString();
                        
                        if (gesture != "none")
                        {
                            await _hubContext.Clients.All.SendAsync("GestureDetected", new
                            {
                                tableId,
                                gesture,
                                timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing table sensor data");
            }
        }

        private async Task ProcessEnvironmentalData(string topic, string payload)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                var roomId = ExtractIdFromTopic(topic, 2);

                if (data.TryGetProperty("environment", out var env))
                {
                    var comfort = data.GetProperty("comfort");
                    
                    await _hubContext.Clients.All.SendAsync("EnvironmentUpdate", new
                    {
                        roomId,
                        temperature = env.GetProperty("temperature").GetDouble(),
                        humidity = env.GetProperty("humidity").GetDouble(),
                        lightLevel = env.GetProperty("lightLevel").GetInt32(),
                        noiseLevel = env.GetProperty("noiseLevel").GetInt32(),
                        comfortIndex = comfort.GetProperty("index").GetDouble(),
                        recommendations = comfort.GetProperty("recommendations").ToString(),
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing environmental data");
            }
        }

        private async Task ProcessBiometricData(string topic, string payload)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                var playerId = ExtractIdFromTopic(topic, 2);

                if (data.TryGetProperty("vitals", out var vitals))
                {
                    var heartRate = vitals.GetProperty("heartRate").GetInt32();
                    var stressLevel = vitals.GetProperty("stressLevel").GetDouble();
                    var focusScore = vitals.GetProperty("focusScore").GetDouble();

                    // Alert if stress is high or focus is low
                    if (stressLevel > 0.7 || focusScore < 0.4)
                    {
                        await _hubContext.Clients.All.SendAsync("PlayerAlert", new
                        {
                            playerId,
                            type = "wellness",
                            heartRate,
                            stressLevel,
                            focusScore,
                            message = GenerateWellnessMessage(stressLevel, focusScore),
                            timestamp = DateTime.UtcNow
                        });
                    }

                    // Broadcast player metrics
                    await _hubContext.Clients.All.SendAsync("PlayerMetrics", new
                    {
                        playerId,
                        heartRate,
                        stressLevel,
                        focusScore,
                        timestamp = DateTime.UtcNow
                    });
                }

                // Process eye tracking for focus areas
                if (data.TryGetProperty("eyeTracking", out var eyeTracking))
                {
                    var focusArea = eyeTracking.GetProperty("focusArea").GetString();
                    var gazePoint = eyeTracking.GetProperty("gazePoint");
                    
                    await _hubContext.Clients.All.SendAsync("EyeTrackingUpdate", new
                    {
                        playerId,
                        focusArea,
                        gazeX = gazePoint.GetProperty("x").GetInt32(),
                        gazeY = gazePoint.GetProperty("y").GetInt32(),
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing biometric data");
            }
        }

        private async Task ProcessControllerInput(string topic, string payload)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                var controllerId = ExtractIdFromTopic(topic, 2);

                if (data.TryGetProperty("input", out var input))
                {
                    var buttons = input.GetProperty("buttons");
                    var analog = input.GetProperty("analog");

                    // Convert controller input to puzzle actions
                    if (buttons.GetProperty("select").GetBoolean())
                    {
                        await _hubContext.Clients.All.SendAsync("ControllerAction", new
                        {
                            controllerId,
                            action = "select",
                            timestamp = DateTime.UtcNow
                        });
                    }

                    if (buttons.GetProperty("rotate").GetBoolean())
                    {
                        await _hubContext.Clients.All.SendAsync("ControllerAction", new
                        {
                            controllerId,
                            action = "rotate",
                            timestamp = DateTime.UtcNow
                        });
                    }

                    // Send analog stick data for piece movement
                    var x = analog.GetProperty("x").GetDouble();
                    var y = analog.GetProperty("y").GetDouble();
                    
                    if (Math.Abs(x) > 0.1 || Math.Abs(y) > 0.1)
                    {
                        await _hubContext.Clients.All.SendAsync("ControllerMove", new
                        {
                            controllerId,
                            x,
                            y,
                            pressure = analog.GetProperty("pressure").GetDouble(),
                            timestamp = DateTime.UtcNow
                        });
                    }
                }

                // Check battery status
                if (data.TryGetProperty("battery", out var battery))
                {
                    var level = battery.GetProperty("level").GetInt32();
                    if (level < 20)
                    {
                        await _hubContext.Clients.All.SendAsync("ControllerBatteryLow", new
                        {
                            controllerId,
                            batteryLevel = level,
                            timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing controller input");
            }
        }

        private async Task ProcessStorageStatus(string topic, string payload)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(payload);
                var boxId = ExtractIdFromTopic(topic, 2);

                if (data.TryGetProperty("inventory", out var inventory))
                {
                    var missingPieces = inventory.GetProperty("missingPieces").GetInt32();
                    
                    if (missingPieces > 0)
                    {
                        await _hubContext.Clients.All.SendAsync("PuzzleInventoryAlert", new
                        {
                            boxId,
                            missingPieces,
                            totalPieces = inventory.GetProperty("totalPieces").GetInt32(),
                            timestamp = DateTime.UtcNow
                        });
                    }
                }

                // Broadcast storage status
                await _hubContext.Clients.All.SendAsync("StorageStatusUpdate", new
                {
                    boxId,
                    status = data.GetProperty("status").ToString(),
                    inventory = data.GetProperty("inventory").ToString(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing storage status");
            }
        }

        private async Task ProcessSystemMessage(string topic, string payload)
        {
            try
            {
                _logger.LogInformation("System message on {Topic}: {Payload}", topic, payload);
                
                // Broadcast system metrics
                await _hubContext.Clients.All.SendAsync("MqttSystemMessage", new
                {
                    topic,
                    payload,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing system message");
            }
        }

        private string ExtractIdFromTopic(string topic, int position)
        {
            var parts = topic.Split('/');
            return parts.Length > position ? parts[position] : "unknown";
        }

        private string GenerateWellnessMessage(double stress, double focus)
        {
            if (stress > 0.8)
                return "High stress detected. Consider taking a short break.";
            if (focus < 0.3)
                return "Low focus detected. Try adjusting lighting or taking a brief walk.";
            if (stress > 0.7 && focus < 0.5)
                return "Elevated stress and reduced focus. A 5-minute break is recommended.";
            
            return "Player wellness metrics require attention.";
        }
    }
}