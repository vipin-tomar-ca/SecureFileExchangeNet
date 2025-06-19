
using FileProcessorService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using SecureFileExchange.Contracts;
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
builder.Services.AddScoped<IFileProcessorService, Services.FileProcessorService>();

// Add gRPC client
builder.Services.AddGrpcClient<BusinessRulesService.BusinessRulesServiceClient>(options =>
{
    options.Address = new Uri("http://localhost:5001");
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
