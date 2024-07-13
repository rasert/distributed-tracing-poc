package main

import (
	"fmt"
	"net/http"

	"github.com/gin-gonic/gin"
)

func main() {
	r := gin.Default()
	r.POST("/save-text", saveTextHandler)
	r.Run(":8080")
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
