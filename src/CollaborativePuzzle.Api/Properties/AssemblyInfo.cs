using System.Runtime.CompilerServices;

// Allow test projects to access internal members for better testability
[assembly: InternalsVisibleTo("CollaborativePuzzle.Tests")]
[assembly: InternalsVisibleTo("CollaborativePuzzle.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // For Moq