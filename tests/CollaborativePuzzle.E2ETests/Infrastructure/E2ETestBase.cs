using Microsoft.Playwright;
using Xunit;

namespace CollaborativePuzzle.E2ETests.Infrastructure;

public class E2ETestBase : IAsyncLifetime
{
    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected string BaseUrl { get; set; } = "http://localhost:5000";

    public virtual async Task InitializeAsync()
    {
        // Install Playwright
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        
        // Launch browser
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox" }
        });

        // Create context with permissions
        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            Permissions = new[] { "microphone", "camera" } // For WebRTC tests
        });

        // Create page
        Page = await Context.NewPageAsync();

        // Enable console logging for debugging
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                Console.WriteLine($"[Browser Error] {msg.Text}");
            }
        };
    }

    public virtual async Task DisposeAsync()
    {
        if (Page != null) await Page.CloseAsync();
        if (Context != null) await Context.CloseAsync();
        if (Browser != null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }

    protected async Task WaitForSignalRConnection()
    {
        // Wait for SignalR to establish connection
        await Page.WaitForFunctionAsync(@"() => {
            return window.signalRConnection && 
                   window.signalRConnection.state === 'Connected';
        }", new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    protected async Task<T> EvaluateAsync<T>(string expression)
    {
        return await Page.EvaluateAsync<T>(expression);
    }

    protected async Task TakeScreenshotOnFailure(string testName)
    {
        var screenshotPath = $"test-results/screenshots/{testName}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
        Console.WriteLine($"Screenshot saved: {screenshotPath}");
    }
}