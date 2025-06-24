// === OPENTELEMETRY IMPORTS ===
using Confluent.Kafka.Extensions.OpenTelemetry;  // Kafka instrumentation for OpenTelemetry
using Consumer.Api;                              // Local instrumentation configuration
using OpenTelemetry.Exporter;                    // OTLP exporter configuration
using OpenTelemetry.Logs;                        // OpenTelemetry logging integration
using OpenTelemetry.Metrics;                     // OpenTelemetry metrics collection
using OpenTelemetry.Resources;                   // Service resource definition
using OpenTelemetry.Trace;                       // OpenTelemetry tracing functionality

var builder = WebApplication.CreateBuilder(args);

// === JAEGER CONFIGURATION ===
// Load Jaeger configuration from appsettings.json
string? jaegerHost = builder.Configuration["Jaeger:Host"];
string? jaegerPort = builder.Configuration["Jaeger:Port"];
string jaegerEndpoint = $"http://{jaegerHost}:{jaegerPort}/v1";

// === OPENTELEMETRY LOGGING CONFIGURATION ===
// Configure OpenTelemetry logging to send logs to Jaeger
builder.Logging.AddOpenTelemetry(options =>
{
    options
        // Set service resource information (name and version)
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(Instrumentation.ServiceName, serviceVersion: Instrumentation.ServiceVersion))
        // Export logs via OTLP protocol to Jaeger
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/logs");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
});

// === OPENTELEMETRY MAIN CONFIGURATION ===
builder.Services.AddOpenTelemetry()
    // Configure service resource metadata
    .ConfigureResource(resource => resource.AddService(Instrumentation.ServiceName, serviceVersion: Instrumentation.ServiceVersion))
    // === TRACING CONFIGURATION ===
    .WithTracing(tracing => tracing
        .AddSource(Instrumentation.ServiceName)         // Add custom activity source for manual spans
        .AddAspNetCoreInstrumentation()                 // Automatic HTTP request/response tracing
        .AddHttpClientInstrumentation()                 // Automatic HTTP client calls tracing
        .AddConfluentKafkaInstrumentation()             // Automatic Kafka consumer tracing
        .AddOtlpExporter(options =>                     // Export traces to Jaeger
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    // === METRICS CONFIGURATION ===
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()                 // ASP.NET Core metrics (request count, duration, etc.)
        .AddHttpClientInstrumentation()                 // HTTP client metrics
        .AddOtlpExporter(options =>                     // Export metrics to Jaeger
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/metrics");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

// === DEPENDENCY INJECTION CONFIGURATION ===
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register custom instrumentation class (contains ActivitySource for manual spans)
builder.Services.AddSingleton<Instrumentation>();

// Register Kafka consumer as a background service
builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

// === HTTP PIPELINE CONFIGURATION ===
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// === SAMPLE API ENDPOINT ===
// Sample data for weather forecast endpoint
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Sample endpoint that will be automatically traced by ASP.NET Core instrumentation
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Start the application (Kafka consumer will start automatically as a background service)
app.Run();

// === WEATHER FORECAST DATA MODEL ===
// Sample record for the weather forecast endpoint
// This will be automatically traced when the endpoint is called
internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
