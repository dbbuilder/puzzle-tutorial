using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using System.Text.Json;

namespace CollaborativePuzzle.LoadTests.Scenarios;

public class ApiLoadTest
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public ApiLoadTest(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public Scenario CreateHealthCheckScenario()
    {
        return Scenario.Create("health_check_scenario", async context =>
        {
            var request = Http.CreateRequest("GET", $"{_baseUrl}/health");
            var response = await Http.Send(_httpClient, request);
            
            return response.IsError ? Response.Fail(response.StatusCode.ToString()) : Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(60))
        );
    }

    public Scenario CreatePuzzleApiScenario()
    {
        return Scenario.Create("puzzle_api_scenario", async context =>
        {
            // Create a new puzzle session
            var createSessionRequest = Http.CreateRequest("POST", $"{_baseUrl}/api/sessions")
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    puzzleId = "test-puzzle-1",
                    maxPlayers = 4,
                    isPrivate = false
                }));

            var createResponse = await Http.Send(_httpClient, createSessionRequest);
            if (createResponse.IsError)
                return Response.Fail($"Failed to create session: {createResponse.StatusCode}");

            var sessionData = JsonSerializer.Deserialize<SessionResponse>(createResponse.Payload.Data);
            if (sessionData?.SessionId == null)
                return Response.Fail("Invalid session response");

            // Get session details
            var getSessionRequest = Http.CreateRequest("GET", $"{_baseUrl}/api/sessions/{sessionData.SessionId}");
            var getResponse = await Http.Send(_httpClient, getSessionRequest);
            
            if (getResponse.IsError)
                return Response.Fail($"Failed to get session: {getResponse.StatusCode}");

            // Join session
            var joinRequest = Http.CreateRequest("POST", $"{_baseUrl}/api/sessions/{sessionData.SessionId}/join")
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { userId = $"user-{context.ScenarioInfo.ThreadNumber}" }));

            var joinResponse = await Http.Send(_httpClient, joinRequest);
            
            if (joinResponse.IsError)
                return Response.Fail($"Failed to join session: {joinResponse.StatusCode}");

            // Leave session
            var leaveRequest = Http.CreateRequest("POST", $"{_baseUrl}/api/sessions/{sessionData.SessionId}/leave")
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { userId = $"user-{context.ScenarioInfo.ThreadNumber}" }));

            var leaveResponse = await Http.Send(_httpClient, leaveRequest);

            return leaveResponse.IsError 
                ? Response.Fail($"Failed to leave session: {leaveResponse.StatusCode}") 
                : Response.Ok();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromSeconds(30)),
            Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(60))
        );
    }

    public Scenario CreateMixedLoadScenario()
    {
        var random = new Random();
        
        return Scenario.Create("mixed_load_scenario", async context =>
        {
            var operation = random.Next(3);
            
            switch (operation)
            {
                case 0: // Health check
                    var healthRequest = Http.CreateRequest("GET", $"{_baseUrl}/health");
                    var healthResponse = await Http.Send(_httpClient, healthRequest);
                    return healthResponse.IsError ? Response.Fail("Health check failed") : Response.Ok();
                    
                case 1: // Get puzzles
                    var puzzlesRequest = Http.CreateRequest("GET", $"{_baseUrl}/api/puzzles");
                    var puzzlesResponse = await Http.Send(_httpClient, puzzlesRequest);
                    return puzzlesResponse.IsError ? Response.Fail("Get puzzles failed") : Response.Ok();
                    
                case 2: // Get active sessions
                    var sessionsRequest = Http.CreateRequest("GET", $"{_baseUrl}/api/sessions/active");
                    var sessionsResponse = await Http.Send(_httpClient, sessionsRequest);
                    return sessionsResponse.IsError ? Response.Fail("Get sessions failed") : Response.Ok();
                    
                default:
                    return Response.Ok();
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromSeconds(30)),
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromSeconds(60)),
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(30))
        );
    }

    private class SessionResponse
    {
        public string? SessionId { get; set; }
        public string? PuzzleId { get; set; }
        public int MaxPlayers { get; set; }
    }
}