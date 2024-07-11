require('./tracing');

const express = require('express');
const bodyParser = require('body-parser');
const { Kafka } = require('kafkajs');

const app = express();
const port = 3000;

// Setup Middlewares
app.use(bodyParser.json());

// Setup Routes
app.post('/publish-text', async (req, res) => {
  const { text } = req.body;
  if (!text) {
    return res.status(400).send('Text is required');
  }

  try {
    await producer.send({
      topic: 'text-topic',
      messages: [
        { value: text }
      ],
    });
    res.status(200).send('Message sent to Kafka');
  } catch (error) {
    console.error('Failed to send message:', error);
    res.status(500).send('Failed to send message');
  }
});

// Setup Kafka Producer
const kafka = new Kafka({
  clientId: 'publisher',
  brokers: ['localhost:29092']
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