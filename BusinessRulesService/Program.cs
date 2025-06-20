
using SecureFileExchange.Services;
using SecureFileExchange.VendorConfig;
using Serilog;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure options
builder.Services.Configure<VendorSettings>(builder.Configuration.GetSection("VendorSettings"));

// Add gRPC
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddConsoleExporter());

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();

// Map gRPC services
app.MapGrpcService<BusinessRulesGrpcService>();
app.MapHealthChecks("/health");

// Enable gRPC-Web for browser clients (optional)
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.Run("http://0.0.0.0:5001");
