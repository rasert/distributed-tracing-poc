package abstractions

import (
	"context"
	"persistence-api/models"
)

type TextDocument = models.TextDocument

type TextRepository interface {
	Insert(ctx context.Context, doc *TextDocument) error
	FindByID(ctx context.Context, id string) (*TextDocument, error)
	Update(ctx context.Context, id string, doc *TextDocument) error
	Delete(ctx context.Context, id string) error
}
