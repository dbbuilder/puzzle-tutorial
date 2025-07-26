using CollaborativePuzzle.E2ETests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CollaborativePuzzle.E2ETests.Scenarios;

public class SignalRE2ETests : E2ETestBase
{
    [Fact]
    public async Task Should_Connect_To_SignalR_Hub()
    {
        // Navigate to test page
        await Page.GotoAsync($"{BaseUrl}/test.html");

        // Wait for SignalR connection
        await WaitForSignalRConnection();

        // Verify connection status
        var connectionState = await EvaluateAsync<string>("window.signalRConnection.state");
        connectionState.Should().Be("Connected");
    }

    [Fact]
    public async Task Should_Join_Session_And_Receive_Updates()
    {
        // Navigate to test page
        await Page.GotoAsync($"{BaseUrl}/test.html");
        await WaitForSignalRConnection();

        // Join a session
        await Page.FillAsync("#sessionId", "e2e-test-session");
        await Page.ClickAsync("#joinSessionBtn");

        // Wait for join confirmation
        await Page.WaitForSelectorAsync("text=Joined session successfully", new PageWaitForSelectorOptions
        {
            Timeout = 5000
        });

        // Verify we're in the session
        var sessionInfo = await EvaluateAsync<string>("document.querySelector('#sessionInfo').textContent");
        sessionInfo.Should().Contain("e2e-test-session");
    }

    [Fact]
    public async Task Should_Send_And_Receive_Chat_Messages()
    {
        // Setup two browser contexts to simulate two users
        var context2 = await Browser.NewContextAsync();
        var page2 = await context2.NewPageAsync();

        try
        {
            // User 1 joins session
            await Page.GotoAsync($"{BaseUrl}/test.html");
            await WaitForSignalRConnection();
            await Page.FillAsync("#sessionId", "chat-test-session");
            await Page.ClickAsync("#joinSessionBtn");

            // User 2 joins same session
            await page2.GotoAsync($"{BaseUrl}/test.html");
            await page2.WaitForFunctionAsync("() => window.signalRConnection?.state === 'Connected'");
            await page2.FillAsync("#sessionId", "chat-test-session");
            await page2.ClickAsync("#joinSessionBtn");

            // User 1 sends message
            await Page.FillAsync("#chatMessage", "Hello from User 1!");
            await Page.ClickAsync("#sendChatBtn");

            // User 2 should receive the message
            await page2.WaitForSelectorAsync("text=Hello from User 1!", new PageWaitForSelectorOptions
            {
                Timeout = 5000
            });

            // Verify message appears in chat
            var chatContent = await page2.EvaluateAsync<string>("document.querySelector('#chatMessages').textContent");
            chatContent.Should().Contain("Hello from User 1!");
        }
        finally
        {
            await page2.CloseAsync();
            await context2.CloseAsync();
        }
    }

    [Fact]
    public async Task Should_Handle_Piece_Movement_Updates()
    {
        // Navigate to test page
        await Page.GotoAsync($"{BaseUrl}/test.html");
        await WaitForSignalRConnection();

        // Join session
        await Page.FillAsync("#sessionId", "movement-test-session");
        await Page.ClickAsync("#joinSessionBtn");

        // Move a piece
        await Page.FillAsync("#pieceId", "test-piece-1");
        await Page.FillAsync("#pieceX", "100");
        await Page.FillAsync("#pieceY", "200");
        await Page.ClickAsync("#movePieceBtn");

        // Wait for movement update
        await Page.WaitForFunctionAsync(@"() => {
            const updates = window.pieceUpdates || [];
            return updates.some(u => u.pieceId === 'test-piece-1' && u.x === 100 && u.y === 200);
        }", new PageWaitForFunctionOptions { Timeout = 5000 });

        // Verify piece position was updated
        var pieceData = await EvaluateAsync<dynamic>("window.pieceUpdates[window.pieceUpdates.length - 1]");
        Assert.NotNull(pieceData);
    }

    [Fact]
    public async Task Should_Handle_Connection_Reconnection()
    {
        // Navigate to test page
        await Page.GotoAsync($"{BaseUrl}/test.html");
        await WaitForSignalRConnection();

        // Simulate network interruption
        await Page.Context.SetOfflineAsync(true);
        await Task.Delay(1000);

        // Verify disconnected state
        var disconnectedState = await EvaluateAsync<string>("window.signalRConnection.state");
        disconnectedState.Should().NotBe("Connected");

        // Restore network
        await Page.Context.SetOfflineAsync(false);

        // Wait for reconnection
        await Page.WaitForFunctionAsync(@"() => {
            return window.signalRConnection?.state === 'Connected';
        }", new PageWaitForFunctionOptions { Timeout = 15000 });

        // Verify reconnected
        var reconnectedState = await EvaluateAsync<string>("window.signalRConnection.state");
        reconnectedState.Should().Be("Connected");
    }
}