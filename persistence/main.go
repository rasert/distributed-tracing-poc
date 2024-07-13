package main

import (
	"context"
	"fmt"
	"net/http"

	"github.com/gin-gonic/gin"
	"go.opentelemetry.io/contrib/instrumentation/github.com/gin-gonic/gin/otelgin"
)

func main() {
	SetupTelemetry(context.Background())

	r := gin.Default()
	r.Use(otelgin.Middleware("persistence-api"))
	r.POST("/save-text", saveTextHandler)
	r.Run(":8888")
}

func saveTextHandler(c *gin.Context) {
	var request struct {
		Text string `json:"text"`
	}
	if err := c.ShouldBindJSON(&request); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	// TODO: Save the text to a database

	c.JSON(http.StatusOK, gin.H{"status": fmt.Sprintf("Text '%s' saved", request.Text)})
}
