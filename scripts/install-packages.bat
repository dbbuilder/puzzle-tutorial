@echo off
echo Installing NuGet packages for CollaborativePuzzle.Api...

cd /d "d:\dev2\puzzletutorial\src\CollaborativePuzzle.Api"

dotnet add package Microsoft.AspNetCore.SignalR --version 8.0.0
dotnet add package Microsoft.AspNetCore.SignalR.Redis --version 8.0.0  
dotnet add package StackExchange.Redis --version 2.7.10
dotnet add package Azure.Storage.Blobs --version 12.19.1
dotnet add package Azure.Identity --version 1.10.4
dotnet add package Azure.Security.KeyVault.Secrets --version 4.5.0
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Serilog.Sinks.ApplicationInsights --version 4.0.0
dotnet add package Polly --version 8.2.0
dotnet add package Hangfire.AspNetCore --version 1.8.6
dotnet add package Hangfire.SqlServer --version 1.8.6
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
dotnet add package MessagePack.AspNetCore --version 2.5.129
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add package System.IdentityModel.Tokens.Jwt --version 7.0.3

echo Installing packages for CollaborativePuzzle.Infrastructure...
cd /d "d:\dev2\puzzletutorial\src\CollaborativePuzzle.Infrastructure"

dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package StackExchange.Redis --version 2.7.10
dotnet add package Azure.Storage.Blobs --version 12.19.1
dotnet add package Azure.Identity --version 1.10.4
dotnet add package Azure.Security.KeyVault.Secrets --version 4.5.0
dotnet add package Polly --version 8.2.0
dotnet add package Serilog --version 3.1.1
dotnet add package MQTTnet --version 4.3.1.873
dotnet add package MQTTnet.Extensions.ManagedClient --version 4.3.1.873

echo Installing packages for CollaborativePuzzle.Hubs...
cd /d "d:\dev2\puzzletutorial\src\CollaborativePuzzle.Hubs"

dotnet add package Microsoft.AspNetCore.SignalR.Core --version 8.0.0
dotnet add package MessagePack --version 2.5.129
dotnet add package Serilog --version 3.1.1

echo All packages installed successfully!
pause
