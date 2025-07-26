using CollaborativePuzzle.LoadTests.Scenarios;
using NBomber.CSharp;
using NBomber.Sinks.InfluxDB;

namespace CollaborativePuzzle.LoadTests;

class Program
{
    static void Main(string[] args)
    {
        var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
        Console.WriteLine($"Running load tests against: {baseUrl}");

        // Create test scenarios
        var signalRTests = new SignalRLoadTest(baseUrl);
        var apiTests = new ApiLoadTest(baseUrl);

        // Configure scenarios based on command line args
        var scenarios = new List<Scenario>();

        if (args.Length < 2 || args[1] == "all")
        {
            scenarios.Add(signalRTests.CreatePuzzleSessionScenario());
            scenarios.Add(signalRTests.CreateConcurrentMovementsScenario());
            scenarios.Add(signalRTests.CreateConnectionStressTest());
            scenarios.Add(apiTests.CreateHealthCheckScenario());
            scenarios.Add(apiTests.CreatePuzzleApiScenario());
            scenarios.Add(apiTests.CreateMixedLoadScenario());
        }
        else
        {
            switch (args[1].ToLower())
            {
                case "signalr":
                    scenarios.Add(signalRTests.CreatePuzzleSessionScenario());
                    scenarios.Add(signalRTests.CreateConcurrentMovementsScenario());
                    scenarios.Add(signalRTests.CreateConnectionStressTest());
                    break;
                case "api":
                    scenarios.Add(apiTests.CreateHealthCheckScenario());
                    scenarios.Add(apiTests.CreatePuzzleApiScenario());
                    scenarios.Add(apiTests.CreateMixedLoadScenario());
                    break;
                case "stress":
                    scenarios.Add(signalRTests.CreateConnectionStressTest());
                    scenarios.Add(apiTests.CreateMixedLoadScenario());
                    break;
                default:
                    Console.WriteLine($"Unknown test type: {args[1]}");
                    Console.WriteLine("Available: all, signalr, api, stress");
                    return;
            }
        }

        // Configure reporting
        var influxDbSink = args.Length > 2 && args[2] == "influx"
            ? new InfluxDBSink(url: "http://localhost:8086", database: "nbomber")
            : null;

        // Build and run
        var statsBuilder = NBomberRunner
            .RegisterScenarios(scenarios.ToArray())
            .WithTestSuite("CollaborativePuzzle")
            .WithTestName($"LoadTest_{DateTime.Now:yyyyMMdd_HHmmss}");

        if (influxDbSink != null)
        {
            statsBuilder = statsBuilder.WithReportingSinks(influxDbSink);
        }

        statsBuilder
            .WithReportFolder($"./test-results/load-tests/{DateTime.Now:yyyyMMdd_HHmmss}")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Txt)
            .Run();

        Console.WriteLine("\nLoad test completed. Check the test-results folder for detailed reports.");
    }
}