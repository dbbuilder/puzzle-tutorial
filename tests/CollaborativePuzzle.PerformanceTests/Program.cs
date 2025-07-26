using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CollaborativePuzzle.PerformanceTests.Benchmarks;

namespace CollaborativePuzzle.PerformanceTests;

class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        // Run specific benchmark or all
        if (args.Length > 0)
        {
            switch (args[0].ToLower())
            {
                case "redis":
                    BenchmarkRunner.Run<RedisBenchmarks>(config);
                    break;
                case "signalr":
                    BenchmarkRunner.Run<SignalRBenchmarks>(config);
                    break;
                default:
                    Console.WriteLine($"Unknown benchmark: {args[0]}");
                    Console.WriteLine("Available benchmarks: redis, signalr");
                    break;
            }
        }
        else
        {
            // Run all benchmarks
            BenchmarkRunner.Run(typeof(Program).Assembly, config);
        }
    }
}