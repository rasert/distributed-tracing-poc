package models

type TextDocument struct {
	ID   string `bson:"_id,omitempty"`
	Text string `bson:"text"`
}
