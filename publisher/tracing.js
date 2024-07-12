const { NodeSDK } = require('@opentelemetry/sdk-node');
const { getNodeAutoInstrumentations } = require('@opentelemetry/auto-instrumentations-node');
const { KafkaJsInstrumentation } = require('@opentelemetry/instrumentation-kafkajs');
const { OTLPTraceExporter } = require('@opentelemetry/exporter-trace-otlp-proto');
const { Resource } = require('@opentelemetry/resources');
const { SEMRESATTRS_SERVICE_NAME, SEMRESATTRS_SERVICE_VERSION } = require('@opentelemetry/semantic-conventions');

const {
    envDetectorSync,
    hostDetectorSync,
    processDetectorSync,
} = require("@opentelemetry/resources");

function awaitAttributes(detector) {
    return {
        async detect(config) {
            const resource = detector.detect(config)
            await resource.waitForAsyncAttributes?.()

            return resource
        },
    }
}

const { diag, DiagConsoleLogger, DiagLogLevel } = require('@opentelemetry/api');
// For troubleshooting, set the log level to DiagLogLevel.DEBUG
diag.setLogger(new DiagConsoleLogger(), DiagLogLevel.INFO);

const sdk = new NodeSDK({
    resourceDetectors: [
        awaitAttributes(envDetectorSync),
        awaitAttributes(processDetectorSync),
        awaitAttributes(hostDetectorSync),
    ],
    resource: new Resource({
        [SEMRESATTRS_SERVICE_NAME]: 'publisher',
        [SEMRESATTRS_SERVICE_VERSION]: '1.0.0',
        env: process.env.NODE_ENV || '',
    }),
    traceExporter: new OTLPTraceExporter({
        // optional - default url is http://localhost:4318/v1/traces
        //url: 'http://localhost:9193/v1/traces',
        // optional - collection of custom headers to be sent with each request, empty by default
        headers: {},
    }),
    instrumentations: [
        getNodeAutoInstrumentations({
            '@opentelemetry/instrumentation-fs': {
                enabled: false,
            },
            '@opentelemetry/instrumentation-net': {
                enabled: false,
            }
        }),
        new KafkaJsInstrumentation()
    ],
});

sdk.start();

process.on('SIGTERM', () => {
    sdk.shutdown()
        .then(() => console.log('Tracing terminated'))
        .catch((error) => console.log('Error terminating tracing', error))
        .finally(() => process.exit(0));
});
