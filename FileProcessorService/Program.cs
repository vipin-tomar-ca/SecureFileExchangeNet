
using FileProcessorService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// Register services
builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddScoped<IFileProcessorService, FileProcessorService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
