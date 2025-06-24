package main

import (
	"context"
	"fmt"
	"log"
	"net/http"
	"strings"

	// === LOCAL PACKAGES ===
	"persistence-api/abstractions"  // Repository interfaces
	"persistence-api/repositories"  // MongoDB repository implementation

	// === GIN WEB FRAMEWORK ===
	"github.com/gin-gonic/gin"                                                     // HTTP web framework
	"go.opentelemetry.io/contrib/instrumentation/github.com/gin-gonic/gin/otelgin" // Gin OpenTelemetry middleware
	"go.opentelemetry.io/otel/trace"                                               // OpenTelemetry tracing interface
)

func main() {
	// === OPENTELEMETRY SETUP ===
	// Initialize OpenTelemetry tracing configuration
	// This sets up OTLP exporter to send traces to Jaeger
	tracer := SetupTelemetry(context.Background())

	// === MONGODB CONNECTION ===
	// Initialize MongoDB client connection
	client, err := repositories.InitializeMongoClient("mongodb://root:example@mongo:27017")
	if err != nil {
		log.Fatal(err)
	}
	// Ensure MongoDB connection is closed when application exits
	defer client.Disconnect(context.Background())

	// === REPOSITORY SETUP ===
	// Create repository instance for text document operations
	repo := repositories.NewMongoTextRepository(client, "testdb", "texts")

	// === GIN WEB SERVER SETUP ===
	r := gin.Default()
	
	// Add OpenTelemetry middleware for automatic HTTP request tracing
	// This will create spans for all incoming HTTP requests
	r.Use(otelgin.Middleware("persistence-api"))
	
	// Register POST endpoint for saving text documents
	r.POST("/save-text", saveTextHandler(repo, tracer))
	
	// Start HTTP server on port 8888
	r.Run(":8888")
}

// saveTextHandler creates a Gin handler function for saving text documents
// Demonstrates manual span creation and distributed tracing context propagation
func saveTextHandler(repo abstractions.TextRepository, tracer trace.Tracer) gin.HandlerFunc {
	return func(c *gin.Context) {
		// === MANUAL SPAN CREATION ===
		// Create a custom span to track the entire save operation
		// This span will be a child of the HTTP request span created by otelgin middleware
		// The trace context is automatically propagated from the HTTP headers
		ctx, span := tracer.Start(c.Request.Context(), "manual-saveTextHandler")
		defer span.End() // Ensure span is always closed

		// === REQUEST PARSING ===
		// Parse JSON request body
		var request struct {
			Text string `json:"text"`
		}
		if err := c.ShouldBindJSON(&request); err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
			return
		}

		// Add event to span indicating successful JSON binding
		span.AddEvent("Bind ok")

		// === ERROR SIMULATION ===
		// Simulate error condition for testing error tracing
		if strings.Contains(request.Text, "go error") {
			c.JSON(http.StatusInternalServerError, gin.H{"error": "Request contains 'go error'"})
			return
		}

		// === DATABASE OPERATION ===
		// Save the text to MongoDB database
		// The context (ctx) carries the span information for potential database tracing
		newDoc := &abstractions.TextDocument{Text: request.Text}
		err := repo.Insert(ctx, newDoc)
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}

		// Add event to span indicating successful database save
		span.AddEvent("Document saved")

		// === SUCCESS RESPONSE ===
		c.JSON(http.StatusOK, gin.H{"status": fmt.Sprintf("Text '%s' saved", request.Text)})
	}
}
