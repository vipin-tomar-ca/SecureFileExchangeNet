
using BusinessRulesService;
using SecureFileExchange.Common;
using SecureFileExchange.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddGrpc();
builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

var app = builder.Build();

// Configure gRPC
app.MapGrpcService<BusinessRulesGrpcService>();
app.MapGet("/", () => "BusinessRulesService is running. Use gRPC client to communicate.");

app.Run("http://0.0.0.0:5001");
