package main

import (
	"context"
	"log"

	// === OPENTELEMETRY CORE ===
	"go.opentelemetry.io/otel"                                               // Main OpenTelemetry API
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp"        // OTLP HTTP trace exporter
	"go.opentelemetry.io/otel/propagation"                                   // Context propagation (headers)
	"go.opentelemetry.io/otel/sdk/resource"                                  // Resource definition (service metadata)
	sdktrace "go.opentelemetry.io/otel/sdk/trace"                            // Trace SDK implementation
	semconv "go.opentelemetry.io/otel/semconv/v1.20.0"                       // Semantic conventions for attributes
	"go.opentelemetry.io/otel/trace"                                         // Trace interface

	// === ALTERNATIVE EXPORTER (COMMENTED) ===
	// "go.opentelemetry.io/otel/exporters/stdout/stdouttrace"               // Stdout exporter for debugging
)

// SetupTelemetry configures OpenTelemetry tracing for the persistence service
// Returns a tracer instance that can be used to create manual spans
func SetupTelemetry(ctx context.Context) trace.Tracer {
	// === TRACE EXPORTERS ===
	// Alternative: Stdout exporter for local debugging (commented out)
	// stdout_exporter, err := stdouttrace.New(stdouttrace.WithPrettyPrint())
	// if err != nil {
	// 	log.Fatal(err)
	// }

	// OTLP HTTP exporter - sends traces to Jaeger collector
	// Uses HTTP protocol to send traces to Jaeger's OTLP endpoint
	otlp_exporter, err := otlptracehttp.New(ctx, otlptracehttp.WithEndpointURL("http://jaeger:4318/v1/traces"))
	if err != nil {
		log.Fatal(err)
	}

	// === TRACER PROVIDER CONFIGURATION ===
	// TracerProvider is the central component that manages tracing configuration
	tp := sdktrace.NewTracerProvider(
		// Batch span processor - efficiently sends spans in batches to reduce overhead
		sdktrace.WithBatcher(otlp_exporter),
		// Alternative: Use stdout exporter for debugging
		// sdktrace.WithBatcher(stdout_exporter),
		
		// Resource configuration - defines service metadata attached to all spans
		sdktrace.WithResource(resource.NewWithAttributes(
			semconv.SchemaURL,                           // OpenTelemetry schema version
			semconv.ServiceName("persistence-api"),      // Service name identifier
		)),
	)

	// === GLOBAL OPENTELEMETRY CONFIGURATION ===
	// Set the global tracer provider - used by automatic instrumentations
	otel.SetTracerProvider(tp)
	
	// Configure context propagation - how trace context is passed between services
	// This enables distributed tracing across service boundaries via HTTP headers
	otel.SetTextMapPropagator(
		propagation.NewCompositeTextMapPropagator(
			propagation.TraceContext{}, // W3C Trace Context standard (traceparent, tracestate headers)
			propagation.Baggage{},      // W3C Baggage standard (baggage header)
		),
	)

	// === TRACER CREATION ===
	// Return a tracer instance with the service name
	// This tracer will be used to create manual spans in the application
	return tp.Tracer("persistence-api")
}
