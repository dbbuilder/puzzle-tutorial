using CollaborativePuzzle.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Collaborative Puzzle API", Version = "v1" });
});

// Add SignalR with Redis backplane
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddStackExchangeRedis(redisConnectionString, options =>
{
    options.Configuration.ChannelPrefix = "puzzle-app";
});

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
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "ready" });

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentCors");
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Map endpoints
app.MapControllers();
app.MapHub<PuzzleHub>("/puzzlehub");
app.MapHealthChecks("/health");

// Minimal API endpoints
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/api/puzzle", () => new { message = "Puzzle API is running", timestamp = DateTime.UtcNow })
    .WithName("GetStatus")
    .WithOpenApi();

app.Run();