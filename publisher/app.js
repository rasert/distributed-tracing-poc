// === IMPORTS ===
// CRITICAL: tracing.js MUST be the first import to instrument libraries
require('./tracing');

// Main library imports
const express = require('express');
const bodyParser = require('body-parser');
const { Kafka } = require('kafkajs');
// OpenTelemetry API for manual spans
const opentelemetry = require('@opentelemetry/api');

// Create a tracer with unique name for this service
const tracer = opentelemetry.trace.getTracer('publisher', '1.0.0');

const app = express();
const port = parseInt(process.env.PORT || '8080');

// === MIDDLEWARES ===
app.use(bodyParser.json());

// === ROUTES ===
// Endpoint to publish text to Kafka
app.post('/publish-text', async (req, res) => {
  const { text } = req.body;
  
  // Basic validation
  if (!text) {
    return res.status(400).send('Text is required');
  }

  // Error simulation to test error tracing
  if (text.includes('node error')) {
    return res.status(500).send('Text contains "node error"');
  }  

  try {
    // === MANUAL SPAN ===
    // Create a custom span to track the entire publish operation
    tracer.startActiveSpan('manual-publish-text', async (span) => {
      // Add an event to the span with the text being published
      span.addEvent('publishing text to Kafka', { text });
  
      // Send message to Kafka (automatically instrumented)
      await producer.send({
        topic: 'text-topic',
        messages: [
          { value: text }
        ],
      });
      
      // HTTP success response
      res.status(200).send('Message sent to Kafka');
  
      // End the manual span
      span.end();
    });
  } catch (error) {
    console.error('Failed to send message:', error);
    res.status(500).send('Failed to send message');
  }
});

// === KAFKA CONFIGURATION ===
// Setup Kafka Producer
const kafka = new Kafka({
  clientId: 'publisher',
  brokers: ['kafka1:9092']
});

const producer = kafka.producer();

// Connect to Kafka on application startup
const runProducer = async () => {
  await producer.connect();
};

runProducer().catch(console.error);

// === SERVER INITIALIZATION ===
// Start Server
app.listen(port, () => {
  console.log(`Server is running on http://localhost:${port}`);
});

// === GRACEFUL SHUTDOWN ===
// Close producer when the application is terminated
process.on('SIGINT', async () => {
  await producer.disconnect();
  process.exit(0);
});