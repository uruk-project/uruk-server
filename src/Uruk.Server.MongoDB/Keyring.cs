using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    internal class Keyring
    {
        public Keyring(ObjectId id, string iss, BsonDocument[] keys)
        {
            ID = id;
            Iss = iss;
            Keys = keys;
        }

        [BsonId]
        public ObjectId ID { get; set; }

        [BsonRequired]
        [BsonElement("iss")]
        public string Iss { get; set; }

        [BsonRequired]
        [BsonElement("keys")]
        public BsonDocument[] Keys { get; set; }
    }
}