using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Uruk.Server.MongoDB
{
    internal class Keyring
    {
        public Keyring(string iss, BsonDocument keys)
        {
            Iss = iss;
            Keys = keys;
        }

        [BsonRequired]
        [BsonElement("iss")]
        public string Iss { get; set; }

        [BsonRequired]
        [BsonElement("keys")]
        public BsonDocument Keys { get; set; }
    }
}