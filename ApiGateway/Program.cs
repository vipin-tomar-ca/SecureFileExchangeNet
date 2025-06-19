
using Serilog;
using OpenTelemetry.Trace;
using SecureFileExchange.Common;
using SecureFileExchange.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

// Add health checks
builder.Services.AddHealthChecks();

// Register application services
var serializerType = builder.Configuration["MessageSerializer:Type"];
if (serializerType == "Protobuf")
{
    builder.Services.AddSingleton<IMessageSerializer, ProtobufMessageSerializer>();
}
else
{
    builder.Services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
}

builder.Services.AddScoped<ISftpService, SftpService>();
builder.Services.AddScoped<IFileProcessorService, FileProcessorService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run("http://0.0.0.0:5000");
