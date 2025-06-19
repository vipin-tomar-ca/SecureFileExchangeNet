
using SftpWorkerService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.VendorConfig;
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
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddScoped<ISftpService, SftpService>();
builder.Services.Configure<VendorSettings>(builder.Configuration.GetSection("VendorSettings"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
