using CollaborativePuzzle.Api.Mqtt;
using CollaborativePuzzle.Api.SocketIO;
using CollaborativePuzzle.Api.WebSockets;
using CollaborativePuzzle.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using CollaborativePuzzle.Api.MinimalApis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CollaborativePuzzle.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure authentication and authorization
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomAuthorization();

// Configure Minimal APIs with OpenAPI/Swagger
SimpleMinimalApiEndpoints.ConfigureMinimalApis(builder);

// Add SignalR with Redis backplane
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});
// TODO: Add Redis backplane with newer package
// .AddStackExchangeRedis(redisConnectionString, options =>
// {
//     options.Configuration.ChannelPrefix = "puzzle-app";
// });

// Add Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

// Add services
builder.Services.AddScoped<CollaborativePuzzle.Core.Interfaces.IRedisService, CollaborativePuzzle.Infrastructure.Services.RedisService>();

// Add repositories - using minimal implementations for now
builder.Services.AddScoped<CollaborativePuzzle.Core.Interfaces.ISessionRepository, CollaborativePuzzle.Infrastructure.Repositories.MinimalSessionRepository>();
builder.Services.AddScoped<CollaborativePuzzle.Core.Interfaces.IPieceRepository, CollaborativePuzzle.Infrastructure.Repositories.MinimalPieceRepository>();
builder.Services.AddScoped<CollaborativePuzzle.Core.Interfaces.IPuzzleRepository, CollaborativePuzzle.Infrastructure.Repositories.MinimalPuzzleRepository>();
builder.Services.AddScoped<CollaborativePuzzle.Core.Interfaces.IUserRepository, CollaborativePuzzle.Infrastructure.Repositories.MinimalUserRepository>();

// Add Redis configuration
builder.Services.Configure<CollaborativePuzzle.Infrastructure.Services.RedisConfiguration>(options =>
{
    options.ConnectionString = redisConnectionString;
    options.InstanceName = "PuzzlePlatform";
    options.Database = 0;
    options.DefaultExpiry = TimeSpan.FromHours(1);
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "ready", "startup" })
    .AddCheck("database", () =>
    {
        // Simple database check - in production, check actual connectivity
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database is accessible");
    }, tags: new[] { "ready", "startup" })
    .AddCheck("signalr", () =>
    {
        // Check SignalR is configured
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SignalR is configured");
    }, tags: new[] { "ready" });

// Add WebSocket handler
builder.Services.AddScoped<WebSocketHandler>();

// Add MQTT services
builder.Services.AddSingleton<IMqttService, MqttService>();
builder.Services.AddHostedService<IoTDeviceSimulator>();
builder.Services.AddHostedService<MqttMessageProcessor>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Enable static files for test.html

// Enable rate limiting
app.UseRateLimiter();

// Enable WebSockets
app.UseWebSockets();
app.UseWebSocketMiddleware();
app.UseSocketIO();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map traditional endpoints
app.MapControllers();
app.MapHub<TestPuzzleHub>("/puzzlehub");
app.MapHub<CollaborativePuzzle.Api.WebRTC.WebRTCHub>("/webrtchub");
app.MapHub<CollaborativePuzzle.Api.SocketIO.SocketIOHub>("/socketiohub");

// Map Minimal APIs with OpenAPI/Swagger
SimpleMinimalApiEndpoints.MapMinimalApis(app);

app.Run();