
using EmailNotificationService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using Serilog;
using Serilog.Extensions.Logging;

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
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
