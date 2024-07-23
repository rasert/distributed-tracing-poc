# Distributed-Tracing POC

This is a simple POC to demonstrate how to implement distributed tracing in a microservices architecture using OpenTelemetry and Jaeger.
The aim of this project is to show that it is possible to keep good observability even with different technologies working together in a distributed system, thanks to OpenTelemetry and OTLP (opentelemetry protocol).

## Architecture

The architecture of the POC is composed of 3 services:
- **Publisher**: A simple service that publishes a message to a Kafka topic (NodeJS + Express);
- **Consumer**: A simple service that consumes messages from a Kafka topic and sends HTTP requests to Persistence service (C# + AspNetCore);
- **Persistence**: A simple service that persists messages sent through HTTP in a MongoDB database (Go + Gin-Gonic);

## Data Processing Flow

If you send an arbitrary text to `/publish-text` endpoint in `Publisher service`, the process will flow normally until the text is saved to the MongoDB. The publisher service will send the text to the `text-topic` in Kafka, then the `Consumer service` will consume it and send a POST request to the `Persistence service` to save the text in the database.

**/publish-text endpoint payload example:**

```json
{
    "text": "This is a sample text."
}
```

## Use Cases

In order to demonstrate error scenarios, the POC has three use cases besides the normal flow:

- **Publisher Error**: if you send a text that contains `node error` to the `/publish-text` endpoint, the publisher service will return `Internal Server Error (500)` and the process will stop there;
- **Consumer Error**: if you send a text that contains `.net error` to the `/publish-text` endpoint, the consumer service will skip the text processing and the process will stop there;
- **Persistence Error**: if you send a text that contains `go error` to the `/publish-text` endpoint, the persistence service will return `Internal Server Error (500)` and the process will stop there;