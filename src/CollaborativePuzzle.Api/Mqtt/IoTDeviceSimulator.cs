using System.Text.Json;
using System.Timers;

namespace CollaborativePuzzle.Api.Mqtt
{
    /// <summary>
    /// Simulates various IoT devices sending data via MQTT
    /// </summary>
    public class IoTDeviceSimulator : IHostedService, IDisposable
    {
        private readonly IMqttService _mqttService;
        private readonly ILogger<IoTDeviceSimulator> _logger;
        private readonly Random _random = new();
        private readonly List<System.Timers.Timer> _timers = new();
        private bool _isRunning;

        public IoTDeviceSimulator(IMqttService mqttService, ILogger<IoTDeviceSimulator> logger)
        {
            _mqttService = mqttService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting IoT device simulator");
            _isRunning = true;

            // Connect to MQTT broker
            await _mqttService.ConnectAsync();

            // Wait for connection
            await Task.Delay(2000, cancellationToken);

            if (_mqttService.IsConnected)
            {
                StartDeviceSimulations();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping IoT device simulator");
            _isRunning = false;

            foreach (var timer in _timers)
            {
                timer?.Stop();
                timer?.Dispose();
            }
            _timers.Clear();

            return Task.CompletedTask;
        }

        private void StartDeviceSimulations()
        {
            // 1. Puzzle Table Sensors (every 2 seconds)
            var tableTimer = new System.Timers.Timer(2000);
            tableTimer.Elapsed += async (s, e) => await SimulateTableSensors();
            tableTimer.Start();
            _timers.Add(tableTimer);

            // 2. Environmental Sensors (every 10 seconds)
            var envTimer = new System.Timers.Timer(10000);
            envTimer.Elapsed += async (s, e) => await SimulateEnvironmentalSensors();
            envTimer.Start();
            _timers.Add(envTimer);

            // 3. Player Biometrics (every 5 seconds)
            var bioTimer = new System.Timers.Timer(5000);
            bioTimer.Elapsed += async (s, e) => await SimulatePlayerBiometrics();
            bioTimer.Start();
            _timers.Add(bioTimer);

            // 4. Smart Puzzle Box (every 30 seconds)
            var boxTimer = new System.Timers.Timer(30000);
            boxTimer.Elapsed += async (s, e) => await SimulateSmartPuzzleBox();
            boxTimer.Start();
            _timers.Add(boxTimer);

            // 5. Game Controller (every 1 second when active)
            var controllerTimer = new System.Timers.Timer(1000);
            controllerTimer.Elapsed += async (s, e) => await SimulateGameController();
            controllerTimer.Start();
            _timers.Add(controllerTimer);

            _logger.LogInformation("Started all IoT device simulations");
        }

        private async Task SimulateTableSensors()
        {
            if (!_isRunning || !_mqttService.IsConnected) return;

            try
            {
                var tableId = "table-001";
                var data = new
                {
                    deviceId = tableId,
                    timestamp = DateTime.UtcNow,
                    sensors = new
                    {
                        pressure = new
                        {
                            zones = GeneratePressureZones(),
                            totalWeight = _random.Next(0, 1000) / 10.0, // 0-100 grams
                            activePieces = _random.Next(0, 20)
                        },
                        touch = new
                        {
                            points = GenerateTouchPoints(),
                            gestures = DetectGestures()
                        },
                        rfid = new
                        {
                            detectedPieces = GenerateRfidPieces(),
                            lastScanned = DateTime.UtcNow.AddSeconds(-_random.Next(0, 60))
                        }
                    }
                };

                await _mqttService.PublishAsync($"puzzle/table/{tableId}/sensors", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating table sensors");
            }
        }

        private async Task SimulateEnvironmentalSensors()
        {
            if (!_isRunning || !_mqttService.IsConnected) return;

            try
            {
                var roomId = "room-main";
                var data = new
                {
                    deviceId = $"env-sensor-{roomId}",
                    timestamp = DateTime.UtcNow,
                    environment = new
                    {
                        temperature = 20 + _random.Next(-5, 5) + _random.NextDouble(),
                        humidity = 45 + _random.Next(-10, 10) + _random.NextDouble(),
                        lightLevel = _random.Next(100, 1000), // lux
                        noiseLevel = _random.Next(30, 70), // dB
                        airQuality = _random.Next(50, 150) // AQI
                    },
                    comfort = new
                    {
                        index = CalculateComfortIndex(),
                        recommendations = GetComfortRecommendations()
                    }
                };

                await _mqttService.PublishAsync($"puzzle/environment/{roomId}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating environmental sensors");
            }
        }

        private async Task SimulatePlayerBiometrics()
        {
            if (!_isRunning || !_mqttService.IsConnected) return;

            try
            {
                var playerId = $"player-{_random.Next(1, 5):D3}";
                var isActive = _random.Next(100) > 20; // 80% chance of being active

                if (isActive)
                {
                    var data = new
                    {
                        deviceId = $"biometric-{playerId}",
                        timestamp = DateTime.UtcNow,
                        player = playerId,
                        vitals = new
                        {
                            heartRate = 60 + _random.Next(0, 40),
                            heartRateVariability = _random.Next(20, 60),
                            stressLevel = _random.Next(1, 10) / 10.0,
                            focusScore = _random.Next(40, 100) / 100.0
                        },
                        eyeTracking = new
                        {
                            gazePoint = new { x = _random.Next(0, 1920), y = _random.Next(0, 1080) },
                            pupilDilation = 3.0 + _random.NextDouble() * 2,
                            blinkRate = _random.Next(10, 30),
                            focusArea = DetermineFocusArea()
                        },
                        posture = new
                        {
                            slouching = _random.Next(100) > 70,
                            headTilt = _random.Next(-30, 30),
                            distance = _random.Next(40, 80) // cm from screen
                        }
                    };

                    await _mqttService.PublishAsync($"puzzle/player/{playerId}/biometrics", data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating player biometrics");
            }
        }

        private async Task SimulateSmartPuzzleBox()
        {
            if (!_isRunning || !_mqttService.IsConnected) return;

            try
            {
                var boxId = "puzzle-box-001";
                var data = new
                {
                    deviceId = boxId,
                    timestamp = DateTime.UtcNow,
                    status = new
                    {
                        isOpen = _random.Next(100) > 50,
                        batteryLevel = _random.Next(20, 100),
                        temperature = 25 + _random.NextDouble() * 5,
                        humidity = 40 + _random.NextDouble() * 20
                    },
                    inventory = new
                    {
                        totalPieces = 1000,
                        missingPieces = _random.Next(0, 5),
                        sortedCompartments = 12,
                        lastInventoryCheck = DateTime.UtcNow.AddHours(-_random.Next(1, 24))
                    },
                    security = new
                    {
                        locked = _random.Next(100) > 30,
                        lastAccess = DateTime.UtcNow.AddMinutes(-_random.Next(0, 120)),
                        accessedBy = $"user-{_random.Next(1, 10):D3}"
                    }
                };

                await _mqttService.PublishAsync($"puzzle/storage/{boxId}/status", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating smart puzzle box");
            }
        }

        private async Task SimulateGameController()
        {
            if (!_isRunning || !_mqttService.IsConnected) return;

            try
            {
                var controllerId = $"controller-{_random.Next(1, 3):D3}";
                var isActive = _random.Next(100) > 40; // 60% chance of being active

                if (isActive)
                {
                    var data = new
                    {
                        deviceId = controllerId,
                        timestamp = DateTime.UtcNow,
                        input = new
                        {
                            buttons = new
                            {
                                select = _random.Next(100) > 90,
                                rotate = _random.Next(100) > 80,
                                zoom = _random.Next(100) > 85,
                                menu = _random.Next(100) > 95
                            },
                            analog = new
                            {
                                x = (_random.NextDouble() - 0.5) * 2, // -1 to 1
                                y = (_random.NextDouble() - 0.5) * 2,
                                pressure = _random.NextDouble()
                            },
                            motion = new
                            {
                                accelerometer = new { x = _random.NextDouble(), y = _random.NextDouble(), z = _random.NextDouble() },
                                gyroscope = new { pitch = _random.Next(-180, 180), roll = _random.Next(-180, 180), yaw = _random.Next(-180, 180) }
                            }
                        },
                        haptic = new
                        {
                            enabled = true,
                            intensity = _random.Next(0, 100) / 100.0,
                            pattern = SelectHapticPattern()
                        },
                        battery = new
                        {
                            level = _random.Next(10, 100),
                            charging = _random.Next(100) > 80
                        }
                    };

                    await _mqttService.PublishAsync($"puzzle/controller/{controllerId}/input", data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating game controller");
            }
        }

        // Helper methods
        private object GeneratePressureZones()
        {
            var zones = new List<object>();
            for (int i = 0; i < 4; i++)
            {
                zones.Add(new
                {
                    zone = i + 1,
                    pressure = _random.Next(0, 100) / 100.0,
                    active = _random.Next(100) > 50
                });
            }
            return zones;
        }

        private object GenerateTouchPoints()
        {
            var points = new List<object>();
            var touchCount = _random.Next(0, 3);
            for (int i = 0; i < touchCount; i++)
            {
                points.Add(new
                {
                    id = i,
                    x = _random.Next(0, 1000),
                    y = _random.Next(0, 1000),
                    pressure = _random.NextDouble()
                });
            }
            return points;
        }

        private string DetectGestures()
        {
            var gestures = new[] { "none", "swipe", "pinch", "rotate", "tap", "hold" };
            return gestures[_random.Next(gestures.Length)];
        }

        private object GenerateRfidPieces()
        {
            var pieces = new List<object>();
            var count = _random.Next(0, 10);
            for (int i = 0; i < count; i++)
            {
                pieces.Add(new
                {
                    pieceId = $"piece-{_random.Next(1000, 9999)}",
                    position = new { x = _random.Next(0, 100), y = _random.Next(0, 100) },
                    orientation = _random.Next(0, 360),
                    lastMoved = DateTime.UtcNow.AddSeconds(-_random.Next(0, 300))
                });
            }
            return pieces;
        }

        private double CalculateComfortIndex()
        {
            // Simple comfort index based on temperature, humidity, noise
            var tempScore = 1.0 - Math.Abs(22 - (20 + _random.Next(-5, 5))) / 10.0;
            var humidityScore = 1.0 - Math.Abs(50 - (45 + _random.Next(-10, 10))) / 50.0;
            var noiseScore = 1.0 - (_random.Next(30, 70) - 30) / 40.0;
            
            return Math.Round((tempScore + humidityScore + noiseScore) / 3 * 100) / 100;
        }

        private List<string> GetComfortRecommendations()
        {
            var recommendations = new List<string>();
            
            if (_random.Next(100) > 70)
                recommendations.Add("Adjust room temperature");
            if (_random.Next(100) > 80)
                recommendations.Add("Increase lighting");
            if (_random.Next(100) > 60)
                recommendations.Add("Reduce noise levels");
            
            return recommendations;
        }

        private string DetermineFocusArea()
        {
            var areas = new[] { "center", "top-left", "top-right", "bottom-left", "bottom-right", "edge", "scanning" };
            return areas[_random.Next(areas.Length)];
        }

        private string SelectHapticPattern()
        {
            var patterns = new[] { "click", "vibrate", "pulse", "wave", "knock" };
            return patterns[_random.Next(patterns.Length)];
        }

        public void Dispose()
        {
            foreach (var timer in _timers)
            {
                timer?.Dispose();
            }
            _timers.Clear();
        }
    }
}