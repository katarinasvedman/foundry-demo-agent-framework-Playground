using System.Diagnostics;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// API endpoint for sentiment analysis
app.MapPost("/api/analyze", async (HttpRequest request) =>
{
    try
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
        
        if (data == null || !data.TryGetValue("text", out var text) || string.IsNullOrWhiteSpace(text))
        {
            return Results.BadRequest("Text is required");
        }

        // Find the project root directory by looking for the TestClient project
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = currentDir;
        
        // Walk up the directory tree to find the foundry-demo directory
        while (!Directory.Exists(Path.Combine(projectRoot, "src", "Foundry.Agents.TestClient")) && 
               Directory.GetParent(projectRoot) != null)
        {
            projectRoot = Directory.GetParent(projectRoot)!.FullName;
        }
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-Command \"$env:SentimentAgent__Enabled='true'; $env:SentimentAgent__McpServerUrl='https://apim-love-kapeltol.azure-api.net/sentiment-mcp/mcp'; $env:PROJECT_ENDPOINT='https://persistent-agents-proj-resource.services.ai.azure.com/api/projects/persistent-agents-proj'; dotnet run --project src/Foundry.Agents.TestClient/Foundry.Agents.TestClient.csproj test '{text.Replace("'", "''")}'\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        if (process == null)
        {
            return Results.Problem("Failed to start process");
        }

        var output = new StringBuilder();
        var error = new StringBuilder();

        // Read both stdout and stderr
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdout = await outputTask;
        var stderr = await errorTask;

        var result = new
        {
            exitCode = process.ExitCode,
            output = stdout,
            error = stderr,
            success = process.ExitCode == 0
        };

        return Results.Json(result);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// Server-Sent Events endpoint for real-time streaming
app.MapGet("/api/analyze-stream", async (HttpContext context, string text) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Text parameter is required");
        return;
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    try
    {
        // Find the project root directory by looking for the TestClient project
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = currentDir;
        
        // Walk up the directory tree to find the foundry-demo directory
        while (!Directory.Exists(Path.Combine(projectRoot, "src", "Foundry.Agents.TestClient")) && 
               Directory.GetParent(projectRoot) != null)
        {
            projectRoot = Directory.GetParent(projectRoot)!.FullName;
        }
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-Command \"$env:SentimentAgent__Enabled='true'; $env:SentimentAgent__McpServerUrl='https://apim-love-kapeltol.azure-api.net/sentiment-mcp/mcp'; $env:PROJECT_ENDPOINT='https://persistent-agents-proj-resource.services.ai.azure.com/api/projects/persistent-agents-proj'; dotnet run --project src/Foundry.Agents.TestClient/Foundry.Agents.TestClient.csproj test '{text.Replace("'", "''")}'\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        if (process == null)
        {
            await context.Response.WriteAsync("data: {\"error\": \"Failed to start process\"}\n\n");
            await context.Response.Body.FlushAsync();
            return;
        }

        // Stream output in real-time
        var outputReader = process.StandardOutput;
        var errorReader = process.StandardError;

        var tasks = new List<Task>
        {
            Task.Run(async () =>
            {
                string? line;
                while ((line = await outputReader.ReadLineAsync()) != null)
                {
                    var eventData = JsonSerializer.Serialize(new { type = "stdout", data = line });
                    await context.Response.WriteAsync($"data: {eventData}\n\n");
                    await context.Response.Body.FlushAsync();
                }
            }),
            Task.Run(async () =>
            {
                string? line;
                while ((line = await errorReader.ReadLineAsync()) != null)
                {
                    var eventData = JsonSerializer.Serialize(new { type = "stderr", data = line });
                    await context.Response.WriteAsync($"data: {eventData}\n\n");
                    await context.Response.Body.FlushAsync();
                }
            })
        };

        await Task.WhenAll(tasks);
        await process.WaitForExitAsync();

        // Small delay to ensure all output is processed
        await Task.Delay(100);

        var completionData = JsonSerializer.Serialize(new { type = "complete", exitCode = process.ExitCode });
        await context.Response.WriteAsync($"data: {completionData}\n\n");
        await context.Response.Body.FlushAsync();
        
        // End the stream properly
        await context.Response.CompleteAsync();
    }
    catch (Exception ex)
    {
        var errorData = JsonSerializer.Serialize(new { type = "error", data = ex.Message });
        await context.Response.WriteAsync($"data: {errorData}\n\n");
        await context.Response.Body.FlushAsync();
    }
});

app.Run();