using OpenTelemetry.Metrics; // Add this using directive for OpenTelemetry Metrics
using OpenTelemetry.Trace; // Ensure this using directive is present for OpenTelemetry Tracing
using OpenTelemetry.Resources;
using Serilog;
using SecureFileExchange.VendorConfig; // Ensure this using directive is present for ResourceBuilder
using OpenTelemetry.Instrumentation.AspNetCore;
using SecureFileExchange.Services;
//using OpenTelemetry.Instrumentation.GrpcNetClient;

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

// Add OpenTelemetry Tracing and Metrics
builder.Services.AddOpenTelemetry();
// Add this using directive for AspNetCore instrumentation

// Add OpenTelemetry Tracing and Metrics
builder.Services.AddOpenTelemetry();
 // Add this using directive for GrpcClient instrumentation

// Update the OpenTelemetry Tracing configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BusinessRulesService"))
            .AddAspNetCoreInstrumentation() // Ensure the required package is installed
            //.AddGrpcClientInstrumentation() // Ensure the required package is installed
            .AddConsoleExporter();
    })
    .WithMetrics(metricsBuilder =>
    {
        metricsBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BusinessRulesService"))
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter();
    })//;dotnet add package OpenTelemetry.Instrumentation.GrpcNetClient
    .WithMetrics(metricsBuilder =>
    {
        metricsBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BusinessRulesService"))
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter();
    }) //dotnet add package OpenTelemetry.Instrumentation.AspNetCore

    .WithMetrics(metricsBuilder =>
    {
        metricsBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BusinessRulesService"))
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter();
    });

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
