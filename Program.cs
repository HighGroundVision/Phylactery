using System.Text;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using PuppeteerSharp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var matches = int.Parse(args.ElementAtOrDefault(1) ?? "6");
        var drafts = int.Parse(args.ElementAtOrDefault(2) ?? "3");
        var browserlessTOKEN = Environment.GetEnvironmentVariable("BROWSERLESS_TOKEN");
        var imgurCLIENTKEY = Environment.GetEnvironmentVariable("IMGUR_CLIENT_KEY");

        var browser = await Puppeteer.ConnectAsync(new ConnectOptions() { BrowserWSEndpoint = $"wss://chrome.browserless.io?token={browserlessTOKEN}" });
        try
        {
            var apiClient = new ApiClient(imgurCLIENTKEY);
            var httpClient = new HttpClient();
            var imageEndpoint = new ImageEndpoint(apiClient, httpClient);

            await browser.DefaultContext.OverridePermissionsAsync("https://lotus.highgroundvision.com", new [] { OverridePermission.ClipboardReadWrite });

            var page = await browser.NewPageAsync();
            page.DefaultNavigationTimeout = 0;
            await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 720 });

            await page.GoToAsync($"https://lotus.highgroundvision.com/bd/?default=win-rate");
            
            var markdown = new StringBuilder();
            
            // Loop Through Matches and Drafts
            for (int m = 1; m <= matches; m++)
            {
                markdown.AppendLine("# Match " + m);
                markdown.AppendLine();

                for (int d = 1; d <= drafts; d++)
                {
                    markdown.AppendLine("## Draft " + d);
                    markdown.AppendLine();

                    var generateButton = await page.WaitForSelectorAsync("#generate");
                    await generateButton.ClickAsync();

                    // Wait for Images to load
                    await Task.Delay(TimeSpan.FromSeconds(3));

                    // Get commands
                    var preCommands = await page.QuerySelectorAsync("#commands");
                    var roasterCommands = await preCommands.EvaluateFunctionAsync<string>("_ => _.innerText");            
                    await preCommands.DisposeAsync();

                    // Get Image
                    var roasterElement = await page.WaitForSelectorAsync("#roster");
                    var stream = await roasterElement.ScreenshotStreamAsync(new ScreenshotOptions() { Type = ScreenshotType.Png });
                    await roasterElement.DisposeAsync();

                    // Upload Image
                    var imageUpload = await imageEndpoint.UploadImageAsync(stream);

                    // Create Markdown
                    markdown.AppendLine($"![Draft {d}]({imageUpload.Link})");
                    markdown.AppendLine();
                    markdown.AppendLine($"[{imageUpload.Link}]({imageUpload.Link})");
                    markdown.AppendLine();
                    markdown.AppendLine("```");
                    markdown.AppendLine(roasterCommands);
                    markdown.AppendLine("```");
                    markdown.AppendLine();
                }
            }
            await page.DisposeAsync();

            await File.WriteAllTextAsync("lotus.md", markdown.ToString());

            Console.WriteLine("Hello From Phylactery!");
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            await browser.CloseAsync();
            await browser.DisposeAsync();
        }
        
    }
}