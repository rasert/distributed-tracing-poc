package main

import (
	"context"
	"log"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.26.0"
)

func SetupTelemetry(ctx context.Context) {
	otlp_exporter, err := otlptracehttp.New(ctx, otlptracehttp.WithEndpointURL("http://localhost:4318/v1/traces"))
	if err != nil {
		log.Fatal(err)
	}

	// TODO: fix ServiceName attribute
	tp := trace.NewTracerProvider(
		trace.WithBatcher(otlp_exporter),
		trace.WithResource(resource.NewWithAttributes(
			semconv.ServiceName("persistence-api").Value.AsString(),
		)),
	)

	otel.SetTracerProvider(tp)
	otel.SetTextMapPropagator(
		propagation.NewCompositeTextMapPropagator(
			propagation.TraceContext{},
			propagation.Baggage{},
		),
	)
}
