@echo off
echo Installing remaining NuGet packages...

cd /d "d:\dev2\puzzletutorial\src\CollaborativePuzzle.Api"

dotnet add package StackExchange.Redis
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
dotnet add package Serilog.AspNetCore
dotnet add package Polly
dotnet add package Hangfire.AspNetCore
dotnet add package MessagePack.AspNetCore

echo Packages installed. Now starting implementation...
