'use strict';

const { NodeSDK } = require('@opentelemetry/sdk-node');
const { resources } = require('@opentelemetry/resources');
const { SemanticResourceAttributes } = require('@opentelemetry/semantic-conventions');
const { getNodeAutoInstrumentations } = require('@opentelemetry/auto-instrumentations-node');
const { JaegerExporter } = require('@opentelemetry/exporter-jaeger');

const sdk = new NodeSDK({
  resource: new resources.Resource({
    [SemanticResourceAttributes.SERVICE_NAME]: 'my-web-api',
  }),
  traceExporter: new JaegerExporter({
    endpoint: 'http://localhost:14268/api/traces', // Substitua pelo endpoint do seu Jaeger
  }),
  instrumentations: [getNodeAutoInstrumentations()],
});

sdk.start()
  .then(() => console.log('Tracing initialized'))
  .catch((error) => console.log('Error initializing tracing', error));

process.on('SIGTERM', () => {
  sdk.shutdown()
    .then(() => console.log('Tracing terminated'))
    .catch((error) => console.log('Error terminating tracing', error))
    .finally(() => process.exit(0));
});
