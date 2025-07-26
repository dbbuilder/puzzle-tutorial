using System.Net;
using CollaborativePuzzle.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CollaborativePuzzle.IntegrationTests.Api;

public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(TestcontainersFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Health_Check_Should_Return_Healthy()
    {
        // Act
        var response = await Client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Health_Check_Should_Include_Service_Status()
    {
        // Act
        var response = await Client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.Should().BeSuccessful();
        content.Should().ContainAll("redis", "database");
    }
}