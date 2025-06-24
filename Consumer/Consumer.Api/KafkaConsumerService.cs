// === IMPORTS ===
using Confluent.Kafka;                           // Kafka client library
using Confluent.Kafka.Extensions.Diagnostics;    // Kafka OpenTelemetry extensions
using System.Diagnostics;                        // Activity/Span creation
using System.Text;                               // String encoding for HTTP requests

namespace Consumer.Api
{
    /// <summary>
    /// Background service that consumes messages from Kafka and forwards them to Persistence API
    /// Demonstrates distributed tracing across Kafka consumption and HTTP calls
    /// </summary>
    public class KafkaConsumerService : BackgroundService
    {
        // === DEPENDENCIES ===
        private readonly IConsumer<Ignore, string> _consumer;      // Kafka consumer client
        private readonly ILogger<KafkaConsumerService> _logger;    // Logging service
        private readonly HttpClient _httpClient;                   // HTTP client for Persistence API calls
        private readonly ActivitySource _activitySource;           // OpenTelemetry ActivitySource for manual spans

        /// <summary>
        /// Constructor - Sets up Kafka consumer, HTTP client, and tracing components
        /// </summary>
        public KafkaConsumerService(IConfiguration configuration, ILogger<KafkaConsumerService> logger, Instrumentation instrumentation)
        {
            // === KAFKA CONSUMER CONFIGURATION ===
            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"], // Kafka broker address
                GroupId = "consumer-group",                                 // Consumer group for load balancing
                AutoOffsetReset = AutoOffsetReset.Earliest                  // Start from beginning if no offset
            };

            // Create and configure Kafka consumer
            _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumer.Subscribe("text-topic");  // Subscribe to the topic that Publisher sends to

            _logger = logger;

            // === HTTP CLIENT CONFIGURATION ===
            _httpClient = new HttpClient();

            // Get ActivitySource for manual span creation from injected Instrumentation
            _activitySource = instrumentation.ActivitySource;

            // === PERSISTENCE API CONFIGURATION ===
            // Configure HTTP client base address for Persistence service
            string? persistenceHost = configuration["PersistenceApi:Host"];
            string? persistencePort = configuration["PersistenceApi:Port"];
            _httpClient.BaseAddress = new Uri($"http://{persistenceHost}:{persistencePort}");
        }

        /// <summary>
        /// Main execution method - Continuously consumes messages from Kafka
        /// Demonstrates distributed tracing flow: Kafka -> Processing -> HTTP call
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Yield control to prevent blocking startup
            await Task.Yield();

            // === MAIN CONSUMPTION LOOP ===
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // === KAFKA CONSUMPTION WITH TRACING ===
                    // ConsumeWithInstrumentation automatically creates spans for Kafka consumption
                    // and propagates trace context from the message headers
                    await _consumer.ConsumeWithInstrumentation(async (result, cancelToken) =>
                    {
                        if (!cancelToken.IsCancellationRequested && result != null)
                        {
                            // === MANUAL SPAN CREATION ===
                            // Create a custom span to track message processing
                            // This span will be a child of the Kafka consumption span
                            using Activity? activity = _activitySource.StartActivity("Manual-ConsumeMessage");

                            string text = result?.Message.Value ?? string.Empty;
                            _logger.LogInformation($"Consumed message '{text}' at: '{result?.TopicPartitionOffset}'.");

                            // Add event to the span with processing information
                            activity?.AddEvent(new("Message consumed."));

                            // === ERROR SIMULATION ===
                            // Skip processing if message contains error trigger
                            if (text.Contains(".net error", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogWarning("Ignoring message with '.net error' substring.");
                                return Task.CompletedTask;
                            }

                            // === HTTP CALL TO PERSISTENCE SERVICE ===
                            activity?.AddEvent(new("Sending to Persistence API"));

                            // Create JSON payload for Persistence API
                            var content = new StringContent($"{{ \"text\": \"{text}\" }}", Encoding.UTF8, "application/json");

                            // HTTP call will be automatically traced by HttpClientInstrumentation
                            // Trace context will be propagated via HTTP headers
                            var response = await _httpClient.PostAsync("/save-text", content, cancelToken);

                            if (response.IsSuccessStatusCode)
                            {
                                activity?.AddEvent(new("Persistence API responded OK"));
                                _logger.LogInformation("HTTP POST request was successful.");
                            }
                            else
                            {
                                _logger.LogError($"HTTP POST request failed with status code {response.StatusCode}.");
                            }
                        }
                        return Task.CompletedTask;
                    }, stoppingToken);
                }
                catch (ConsumeException e)
                {
                    _logger.LogError($"Error occured: {e.Error.Reason}");
                }
                catch (HttpRequestException e)
                {
                    _logger.LogError(e, $"Unable to send text to persistence service: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Cleanup method - Properly dispose of Kafka consumer resources
        /// </summary>
        public override void Dispose()
        {
            // Gracefully close Kafka consumer connection
            _consumer.Close();
            _consumer.Dispose();
            base.Dispose();
        }
    }
}
