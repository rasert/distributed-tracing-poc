using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Consumer.Api
{
    public class KafkaConsumerService : BackgroundService
    {
        private readonly IConsumer<Ignore, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly HttpClient _httpClient;

        public KafkaConsumerService(IConfiguration configuration, ILogger<KafkaConsumerService> logger)
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
                            string text = result?.Message.Value ?? string.Empty;
                            _logger.LogInformation($"Consumed message '{text}' at: '{result?.TopicPartitionOffset}'.");

                            var content = new StringContent($"{{ \"text\": \"{text}\" }}", Encoding.UTF8, "application/json");
                            var response = await _httpClient.PostAsync("http://localhost:8888/save-text", content, cancelToken);

                            if (response.IsSuccessStatusCode)
                            {
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
