package main

import (
	"context"
	"fmt"
	"log"
	"net/http"

	"persistence-api/abstractions"
	"persistence-api/repositories"

	"github.com/gin-gonic/gin"
	"go.opentelemetry.io/contrib/instrumentation/github.com/gin-gonic/gin/otelgin"
)

func main() {
	SetupTelemetry(context.Background())

	client, err := repositories.InitializeMongoClient("mongodb://root:example@localhost:27017")
	if err != nil {
		log.Fatal(err)
	}
	defer client.Disconnect(context.Background())

	// Create repository
	repo := repositories.NewMongoTextRepository(client, "testdb", "texts")

	r := gin.Default()
	r.Use(otelgin.Middleware("persistence-api"))
	r.POST("/save-text", saveTextHandler(repo))
	r.Run(":8888")
}

func saveTextHandler(repo abstractions.TextRepository) gin.HandlerFunc {
	return func(c *gin.Context) {
		var request struct {
			Text string `json:"text"`
		}
		if err := c.ShouldBindJSON(&request); err != nil {
			c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
			return
		}

		// Save the text to a database
		newDoc := &abstractions.TextDocument{Text: request.Text}
		err := repo.Insert(c.Request.Context(), newDoc)
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}

		c.JSON(http.StatusOK, gin.H{"status": fmt.Sprintf("Text '%s' saved", request.Text)})
	}
}
