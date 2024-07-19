require('./tracing');

const express = require('express');
const bodyParser = require('body-parser');
const { Kafka } = require('kafkajs');
const opentelemetry = require('@opentelemetry/api');

const tracer = opentelemetry.trace.getTracer('publisher', '1.0.0');

const app = express();
const port = parseInt(process.env.PORT || '8080');

// Setup Middlewares
app.use(bodyParser.json());

// Setup Routes
app.post('/publish-text', async (req, res) => {
  const { text } = req.body;
  if (!text) {
    return res.status(400).send('Text is required');
  }  

  try {
    tracer.startActiveSpan('publish-text', async (span) => {
      span.addEvent('publishing text to Kafka', { text });
  
      await producer.send({
        topic: 'text-topic',
        messages: [
          { value: text }
        ],
      });
      res.status(200).send('Message sent to Kafka');
  
      span.end();
    });
  } catch (error) {
    console.error('Failed to send message:', error);
    res.status(500).send('Failed to send message');
  }
});

// Setup Kafka Producer
const kafka = new Kafka({
  clientId: 'publisher',
  brokers: ['kafka1:29092']
});

const producer = kafka.producer();

const runProducer = async () => {
  await producer.connect();
};

runProducer().catch(console.error);

// Start Server
app.listen(port, () => {
  console.log(`Server is running on http://localhost:${port}`);
});

// Close producer when the application is terminated
process.on('SIGINT', async () => {
  await producer.disconnect();
  process.exit(0);
});