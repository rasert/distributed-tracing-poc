package repositories

import (
	"context"
	"errors"

	"persistence-api/abstractions"
	"persistence-api/models"

	"go.mongodb.org/mongo-driver/bson"
	"go.mongodb.org/mongo-driver/bson/primitive"
	"go.mongodb.org/mongo-driver/mongo"
)

type TextRepository = abstractions.TextRepository
type TextDocument = models.TextDocument

type mongoTextRepository struct {
	collection *mongo.Collection
}

func NewMongoTextRepository(client *mongo.Client, dbName, collectionName string) TextRepository {
	collection := client.Database(dbName).Collection(collectionName)
	return &mongoTextRepository{collection}
}

func (r *mongoTextRepository) Insert(ctx context.Context, doc *models.TextDocument) error {
	_, err := r.collection.InsertOne(ctx, doc)
	return err
}

func (r *mongoTextRepository) FindByID(ctx context.Context, id string) (*models.TextDocument, error) {
	objectID, err := primitive.ObjectIDFromHex(id)
	if err != nil {
		return nil, err
	}

	var doc models.TextDocument
	err = r.collection.FindOne(ctx, bson.M{"_id": objectID}).Decode(&doc)
	if err != nil {
		if err == mongo.ErrNoDocuments {
			return nil, nil
		}
		return nil, err
	}
	return &doc, nil
}

func (r *mongoTextRepository) Update(ctx context.Context, id string, doc *models.TextDocument) error {
	objectID, err := primitive.ObjectIDFromHex(id)
	if err != nil {
		return err
	}

	filter := bson.M{"_id": objectID}
	update := bson.M{"$set": doc}

	result, err := r.collection.UpdateOne(ctx, filter, update)
	if err != nil {
		return err
	}
	if result.MatchedCount == 0 {
		return errors.New("no document found with the given ID")
	}

	return nil
}

func (r *mongoTextRepository) Delete(ctx context.Context, id string) error {
	objectID, err := primitive.ObjectIDFromHex(id)
	if err != nil {
		return err
	}

	filter := bson.M{"_id": objectID}

	result, err := r.collection.DeleteOne(ctx, filter)
	if err != nil {
		return err
	}
	if result.DeletedCount == 0 {
		return errors.New("no document found with the given ID")
	}

	return nil
}
