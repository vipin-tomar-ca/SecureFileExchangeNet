using System.Diagnostics;

Console.WriteLine("Starting Secure File Exchange Platform...");

// Start the API Gateway as the main entry point
var apiGatewayPath = Path.Combine(Directory.GetCurrentDirectory(), "ApiGateway");
var processInfo = new ProcessStartInfo("dotnet", "run")
{
    WorkingDirectory = apiGatewayPath,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};

using var process = Process.Start(processInfo);
if (process != null)
{
    Console.WriteLine($"API Gateway started with PID: {process.Id}");
    await process.WaitForExitAsync();
}
else
{
    Console.WriteLine("Failed to start API Gateway");
}