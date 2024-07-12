using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;

namespace Consumer.Api
{
    public class KafkaConsumerService : BackgroundService
    {
        private readonly IConsumer<Ignore, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;

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
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _consumer.ConsumeWithInstrumentation((result, cancelToken) =>
                    {
                        if (!cancelToken.IsCancellationRequested && result != null)
                        {
                            string text = result?.Message.Value ?? string.Empty;
                            _logger.LogInformation($"Consumed message '{text}' at: '{result?.TopicPartitionOffset}'.");

                            // TODO: Send HTTP request to Golang App

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
