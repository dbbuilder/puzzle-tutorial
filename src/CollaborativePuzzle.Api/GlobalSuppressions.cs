// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress LoggerMessage delegates warning for now - will refactor in future sprint
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", 
    Justification = "Existing logging pattern works well, will consider refactoring in future")]

// Suppress insecure random for non-security critical code
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness", 
    Scope = "member", 
    Target = "~M:CollaborativePuzzle.Api.WebSockets.WebSocketHandler.GenerateBinaryData(System.Int32)~System.Byte[]",
    Justification = "Used for demo data generation, not security sensitive")]

[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness", 
    Scope = "member", 
    Target = "~M:CollaborativePuzzle.Api.Mqtt.IoTDeviceSimulator.SimulateEnvironmentalSensors",
    Justification = "Simulation data only, not security sensitive")]

// Suppress excessive class coupling for Program.cs
[assembly: SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", 
    Scope = "member", 
    Target = "~M:Program.<Main>$(System.String[])",
    Justification = "Program.cs is the composition root and requires many dependencies")]

// Suppress TODO warnings - these are intentional
[assembly: SuppressMessage("Major Code Smell", "S1135:Track uses of \"TODO\" tags", 
    Justification = "TODO comments are tracked in project management system")]