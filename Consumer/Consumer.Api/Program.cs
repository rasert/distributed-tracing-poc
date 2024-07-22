using Confluent.Kafka.Extensions.OpenTelemetry;
using Consumer.Api;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Load Jaeger configuration
string? jaegerHost = builder.Configuration["Jaeger:Host"];
string? jaegerPort = builder.Configuration["Jaeger:Port"];
string jaegerEndpoint = $"http://{jaegerHost}:{jaegerPort}/v1";

builder.Logging.AddOpenTelemetry(options =>
{
    options
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(Instrumentation.ServiceName, serviceVersion: Instrumentation.ServiceVersion))
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/logs");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        });
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(Instrumentation.ServiceName, serviceVersion: Instrumentation.ServiceVersion))
    .WithTracing(tracing => tracing
        .AddSource(Instrumentation.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConfluentKafkaInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{jaegerEndpoint}/metrics");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<Instrumentation>();

builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

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

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
