// === OPENTELEMETRY SETUP ===
// This file configures OpenTelemetry for automatic and manual instrumentation
// IMPORTANT: Must be imported BEFORE any other modules!

// Main OpenTelemetry SDK for Node.js
const { NodeSDK } = require('@opentelemetry/sdk-node');
// Automatic instrumentations for popular libraries (express, http, etc.)
const { getNodeAutoInstrumentations } = require('@opentelemetry/auto-instrumentations-node');
// Specific instrumentation for KafkaJS
const { KafkaJsInstrumentation } = require('@opentelemetry/instrumentation-kafkajs');
// Trace exporter via OTLP protocol (OpenTelemetry Protocol)
const { OTLPTraceExporter } = require('@opentelemetry/exporter-trace-otlp-proto');
// Class to define resources (service metadata)
const { Resource } = require('@opentelemetry/resources');
// Standardized semantic constants for service.name and service.version
const { SEMRESATTRS_SERVICE_NAME, SEMRESATTRS_SERVICE_VERSION } = require('@opentelemetry/semantic-conventions');

// Resource Detectors - automatically collect environment metadata
const {
    envDetectorSync,      // Environment variables (DEPLOYMENT_ENV, K8S_*, etc.)
    hostDetectorSync,     // Host/container information (hostname, OS, arch)
    processDetectorSync,  // Node.js process information (PID, runtime version)
} = require("@opentelemetry/resources");

// Wrapper to await asynchronous attributes from detectors
// Some detectors perform async operations (file reading, network calls)
function awaitAttributes(detector) {
    return {
        async detect(config) {
            const resource = detector.detect(config)
            // Wait for all asynchronous attributes to be resolved
            await resource.waitForAsyncAttributes?.()

            return resource
        },
    }
}

// OpenTelemetry logging configuration for debugging
const { diag, DiagConsoleLogger, DiagLogLevel } = require('@opentelemetry/api');
// For troubleshooting, change to DiagLogLevel.DEBUG
diag.setLogger(new DiagConsoleLogger(), DiagLogLevel.INFO);

// Main OpenTelemetry SDK configuration
const sdk = new NodeSDK({
    // Resource detectors - automatically collect environment metadata
    resourceDetectors: [
        awaitAttributes(envDetectorSync),      // Environment vars
        awaitAttributes(processDetectorSync),  // Node.js process info
        awaitAttributes(hostDetectorSync),     // Host/container info
    ],
    // Custom resources - fixed metadata attached to all traces
    resource: new Resource({
        [SEMRESATTRS_SERVICE_NAME]: 'publisher',  // Service name
        [SEMRESATTRS_SERVICE_VERSION]: '1.0.0',   // Service version
        env: process.env.NODE_ENV || '',          // Environment (dev, prod, etc.)
    }),
    // Trace exporter - where to send collected data
    traceExporter: new OTLPTraceExporter({
        // Jaeger collector URL (OTLP protocol)
        url: 'http://jaeger:4318/v1/traces',
        // Custom headers (authentication, etc.) - empty by default
        headers: {},
    }),
    // Instrumentations - libraries that will be automatically monitored
    instrumentations: [
        // Automatic instrumentation for popular libraries
        getNodeAutoInstrumentations({
            // Disable filesystem instrumentation (too verbose)
            '@opentelemetry/instrumentation-fs': {
                enabled: false,
            },
            // Disable low-level network instrumentation (too verbose)
            '@opentelemetry/instrumentation-net': {
                enabled: false,
            }
        }),
        // Specific instrumentation for KafkaJS (producer/consumer)
        new KafkaJsInstrumentation()
    ],
});

// Start the SDK - activates all automatic instrumentation
// MUST be called BEFORE importing other libraries
sdk.start();

// Graceful shutdown - ensures traces are sent before terminating
process.on('SIGTERM', () => {
    sdk.shutdown()
        .then(() => console.log('Tracing terminated'))
        .catch((error) => console.log('Error terminating tracing', error))
        .finally(() => process.exit(0));
});
