using System;
using System.Collections.Generic;
using System.Linq;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public class MongoDBMerkeTree : IMerkleTree
    {
        private readonly IMongoCollection<MerkleRoot> _roots;
        private readonly IMongoCollection<MerkleNode> _nodes;
        private readonly ILogger<MongoDBMerkeTree> _logger;
        private readonly IMongoClient _client;
        private readonly MongoDBStoreOptions _options;

        public MongoDBMerkeTree(IMongoClient client, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBMerkeTree> logger)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options.Value;
            var db = client.GetDatabase(_options.Database);
            _roots = db.GetCollection<MerkleRoot>("merkle_roots");
            _nodes = db.GetCollection<MerkleNode>("merkle_nodes");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MerkleProof Append(byte[] hash)
        {
            MerkleNode leaf = new MerkleNode(hash);

            var session = _client.StartSession();
            try
            {
                //session.StartTransaction();
                var root = _roots.Find(session, _ => true)
                            .Sort(new SortDefinitionBuilder<MerkleRoot>()
                            .Descending("$natural")).Limit(1).SingleOrDefault();

                if (root is null)
                {
                    _nodes.InsertOne(session, leaf);
                    root = new MerkleRoot(leaf, leaf.Hash);
                    _roots.InsertOne(session, root);
                }
                else
                {
                    _nodes.InsertOne(session, leaf);
                    var stack = new Stack<MerkleNode>(root.Level + 1);
                    var currentNode = _nodes.Find(session, n => n.Id == root.NodeId).Single();
                    MerkleNode node;
                    if (currentNode.IsFull)
                    {
                        node = new MerkleNode(currentNode, leaf);
                        _nodes.InsertOne(session, node);
                    }
                    else
                    {
                        while (!currentNode.IsFull)
                        {
                            var left = _nodes.Find(session, n => n.Id == currentNode.Left).Single();
                            stack.Push(left);
                            var right = _nodes.Find(session, n => n.Id == currentNode.Right).Single();
                            currentNode = right;
                        }

                        stack.Push(currentNode);
                        stack.Push(leaf);
                        var hashTmp = new byte[Sha256.Shared.HashSize];
                        do
                        {
                            var right = stack.Pop();
                            var left = stack.Pop();
                            Sha256.Shared.ComputeHash(right.Hash, (ReadOnlySpan<byte>)left.Hash, hashTmp);
                            node = new MerkleNode(left, right);
                            _nodes.InsertOne(session, node);
                            stack.Push(node);
                        } while (stack.Count != 1);
                    }

                    root = new MerkleRoot(node, node.Hash);
                    _roots.InsertOne(session, root);
                }

                //session.CommitTransaction();
                _logger.LogInformation("The leaf {leafHash} was appended to tree {treeHash}.", ByteToHex(hash), ByteToHex(root.Hash));
                return new MerkleProof(root.Hash);
            }
            catch (Exception e)
            {
                //session.AbortTransaction();
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
}