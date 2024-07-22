using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using System.Diagnostics;
using System.Text;

namespace Consumer.Api
{
    public class KafkaConsumerService : BackgroundService
    {
        private readonly IConsumer<Ignore, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ActivitySource _activitySource;

        public KafkaConsumerService(IConfiguration configuration, ILogger<KafkaConsumerService> logger, Instrumentation instrumentation)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = "consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumer.Subscribe("text-topic");
            _logger = logger;
            _httpClient = new HttpClient();
            _activitySource = instrumentation.ActivitySource;

            string? persistenceHost = configuration["PersistenceApi:Host"];
            string? persistencePort = configuration["PersistenceApi:Port"];
            _httpClient.BaseAddress = new Uri($"http://{persistenceHost}:{persistencePort}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _consumer.ConsumeWithInstrumentation(async (result, cancelToken) =>
                    {
                        if (!cancelToken.IsCancellationRequested && result != null)
                        {
                            // This creates a Span to be sent to the OpenTelemetry Collector.
                            using var activity = _activitySource.StartActivity("ConsumeMessage");

                            string text = result?.Message.Value ?? string.Empty;
                            _logger.LogInformation($"Consumed message '{text}' at: '{result?.TopicPartitionOffset}'.");

                            activity?.AddEvent(new("Message consumed. Sending to Persistence API"));

                            var content = new StringContent($"{{ \"text\": \"{text}\" }}", Encoding.UTF8, "application/json");
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

        public override void Dispose()
        {
            _consumer.Close();
            _consumer.Dispose();
            base.Dispose();
        }
    }
}
