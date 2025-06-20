
using SftpWorkerService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.VendorConfig;
using Serilog;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddConsoleExporter());

// Configure options
builder.Services.Configure<VendorSettings>(builder.Configuration.GetSection("VendorSettings"));

// Register services
builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddSingleton<IEncryptionService>(sp => 
    new AesEncryptionService(builder.Configuration.GetValue<string>("Encryption:Key") ?? ""));
builder.Services.AddScoped<ISftpService, SftpService>();

// Add health checks
builder.Services.AddHealthChecks();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
