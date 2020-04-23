using System;
using System.Linq;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBMerkeTree : IMerkleTree
    {
        private readonly IMongoCollection<MerkleNode> _roots;
        private readonly IMongoCollection<MerkleNode> _nodes;
        private readonly ILogger<MongoDBMerkeTree> _logger;
        private readonly IMongoClient _client;
        private readonly MongoDBStoreOptions _options;

        public MongoDBMerkeTree(IMongoClient client, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBMerkeTree> logger)
        {
            _client = client;
            _options = options.Value;
            var db = client.GetDatabase(_options.Database);
            _roots = db.GetCollection<MerkleNode>("merkle_roots");
            _nodes = db.GetCollection<MerkleNode>("merkle_nodes");
            _logger = logger;
        }

        public byte[] Append(byte[] hash)
        {
            MerkleNode leaf = new MerkleNode(hash);

            var session = _client.StartSession();
            try
            {
                byte[] result;
                _nodes.InsertOne(session, leaf);

                var otherLeaf = _roots.Find(session, n => n.Level == 0).FirstOrDefault();
                if (otherLeaf != null)
                {
                    var node = new MerkleNode(otherLeaf, leaf, 1);
                    _nodes.InsertOne(session, node);
                    _roots.InsertOne(session, node);
                    _roots.DeleteOne(session, n => n.Id == otherLeaf.Id);

                    int level = 1;
                    MerkleNode? rootNode = null;
                    while (true)
                    {
                        long sameLevelCount = _roots.Find(session, n => n.Level == level).CountDocuments();
                        if (sameLevelCount >= 2)
                        {
                            var rootsToMerge = _roots.Find(session, n => n.Level == level).Limit(1 << level).ToList();
                            for (int i = 0; i < sameLevelCount - 1; i += 2)
                            {
                                var tree1 = rootsToMerge[i];
                                var tree2 = rootsToMerge[i + 1];
                                rootNode = new MerkleNode(tree1, tree2, level + 1);
                                _nodes.InsertOne(session, rootNode);
                                var filter = Builders<MerkleNode>.Filter.And(
                                    Builders<MerkleNode>.Filter.Eq(n => n.Level, level),
                                    Builders<MerkleNode>.Filter.Or(
                                        Builders<MerkleNode>.Filter.Eq(n => n.Id, tree1.Id),
                                        Builders<MerkleNode>.Filter.Eq(n => n.Id, tree2.Id)));
                                var deleteResult = _roots.DeleteMany(session, filter);
                                _roots.InsertOne(session, rootNode);
                            }

                            level++;
                        }
                        else
                        {
                            if (rootNode != null)
                            {
                                result = rootNode.Hash;
                            }
                            else
                            {
                                result = node.Hash;
                            }

                            break;
                        }
                    }
                }
                else
                {
                    _roots.InsertOne(session, leaf);
                    result = leaf.Hash;
                }

                session.CommitTransaction();
                _logger.LogInformation("The leaf {leafHash} was appended to tree {treeHash}.", ByteToHex(hash), ByteToHex(result));
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while appending the leaf {leaf}.", ByteToHex(hash));
                throw;
            }
        }

        private static string ByteToHex(ReadOnlySpan<byte> bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }
    }

    public class MerkleNode
    {
        public MerkleNode(ObjectId left, ObjectId right, int level, byte[] hash)
        {
            Left = left;
            Right = right;
            Level = level;
            Hash = hash;
        }

        public MerkleNode(MerkleNode left, MerkleNode right, int level)
        {
            Level = level;
            Hash = new byte[Sha256.Shared.HashSize];
            Sha256.Shared.ComputeHash(right.Hash, (ReadOnlySpan<byte>)left.Hash, Hash);
            Left = left.Id;
            Right = right.Id;
        }

        public MerkleNode(byte[] hash)
        {
            Level = 0;
            Hash = hash;
        }

        [BsonElement("hash")]
        public byte[] Hash { get; }

        [BsonId]
        [BsonElement("id")]
        public ObjectId Id { get; set; }

        [BsonElement("level")]
        public int Level { get; }

        [BsonElement("left")]
        [BsonIgnoreIfDefault]
        public ObjectId Left { get; }

        [BsonElement("right")]
        [BsonIgnoreIfDefault]
        public ObjectId Right { get; }
    }
}