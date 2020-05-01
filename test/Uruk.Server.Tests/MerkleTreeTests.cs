using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Operations;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;
using Moq;
using Uruk.Server.MongoDB;
using Xunit;

namespace Uruk.Server.Tests
{
    public class MerkleTreeTests
    {
        static MerkleTreeTests()
        {
            BsonClassMap.RegisterClassMap<MerkleNode>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(n => new MerkleNode(n.Children, n.Level, n.Hash, n.IsFull));
            });
            BsonClassMap.RegisterClassMap<MerkleRoot>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(n => new MerkleRoot(n.Id, n.NodeId, n.Level, n.Hash));
            });
        }

        [Theory]
        [InlineData(256)]
        public void CreateTree(int count)
        {
            // Ensures the count parameter is a power of 2
            Assert.Equal(0, (count & (count - 1)));

            var nodes = new Dictionary<ObjectId, MerkleNode>();
            var roots = new List<MerkleRoot>();
            var client = CreateClient(nodes, roots);
            var tree = new MongoDBMerkeTree(client, Options.Create(new MongoDBStoreOptions()), new TestLogger<MongoDBMerkeTree>());
            for (int i = 0; i < count; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)i;
                tree.Append(hash);
            }

            Assert.Equal(count, roots.Count);
            for (int i = 0; i < roots.Count; i++)
            {
                int expectedHeight = (int)Math.Ceiling(Math.Log(i + 1, 2));
                Assert.Equal(expectedHeight, roots[i].Level);
            }

            for (int i = 0; i < roots.Count; i++)
            {
                var node = nodes[roots[i].NodeId];
                Assert.NotNull(node);
                AssertRoot(nodes, node);
            }
        }

        private static void AssertRoot(Dictionary<ObjectId, MerkleNode> nodes, MerkleNode node)
        {
            if (node.IsLeaf)
            {
                return;
            }

            var left = nodes[node.Left];
            Assert.NotNull(left);
            var right = nodes[node.Right];
            Assert.NotNull(right);
            Span<byte> expectedHash = stackalloc byte[Sha256.Shared.HashSize];
            Sha256.Shared.ComputeHash(right!.Hash, (ReadOnlySpan<byte>)left!.Hash, expectedHash);
            Assert.True(expectedHash.SequenceEqual(node.Hash));
            //Assert.Equal(node.Level - 1, right.Level);
            //Assert.Equal(node.Level - 1, left.Level);

            AssertRoot(nodes, left);
            AssertRoot(nodes, right);
        }

        private static IMongoClient CreateClient(Dictionary<ObjectId, MerkleNode> nodes, List<MerkleRoot> roots)
        {
            var rootCollection = new Mock<IMongoCollection<MerkleRoot>>(MockBehavior.Strict);
            var nodeCollection = new Mock<IMongoCollection<MerkleNode>>(MockBehavior.Strict);
            var db = new Mock<IMongoDatabase>(MockBehavior.Strict);
            var client = new Mock<IMongoClient>(MockBehavior.Strict);
            var session = new Mock<IClientSessionHandle>(MockBehavior.Strict);

            session.Setup(s => s.CommitTransaction(default));

            client.Setup(c => c.GetDatabase(It.IsAny<string>(), null)).Returns(db.Object);
            client.Setup(c => c.StartSession(null, default))
                .Returns(session.Object);

            db.Setup(d => d.GetCollection<MerkleRoot>("merkle_roots", null)).Returns(rootCollection.Object);
            db.Setup(d => d.GetCollection<MerkleNode>("merkle_nodes", null)).Returns(nodeCollection.Object);

            rootCollection.Setup(r => r.FindSync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleRoot>>(), It.IsAny<FindOptions<MerkleRoot>>(), It.IsAny<CancellationToken>()))
                .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleRoot>, FindOptions<MerkleRoot>, CancellationToken>((s, expr, o, c) => CreateRootCursor(roots));

            nodeCollection.Setup(r => r.FindSync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<FindOptions<MerkleNode>>(), It.IsAny<CancellationToken>()))
                .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleNode>, FindOptions<MerkleNode>, CancellationToken>((s, expr, o, c) => CreateNodeCursor(nodes, expr));

            nodeCollection.Setup(n => n.InsertOne(session.Object, It.IsAny<MerkleNode>(), null, default))
                .Callback<IClientSessionHandle, MerkleNode, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                {
                    node.Id = ObjectId.GenerateNewId();
                    node.Hash = node.Hash.ToArray();
                    nodes.Add(node.Id, node);
                });

            rootCollection.Setup(n => n.InsertOne(session.Object, It.IsAny<MerkleRoot>(), null, default))
                .Callback<IClientSessionHandle, MerkleRoot, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                {
                    node.Id = ObjectId.GenerateNewId();
                    roots.Add(node);
                });

            //var findResult = new Mock<IFindFluent<MerkleNode, MerkleNode>>(MockBehavior.Strict);
            //findResult.Setup(f => f.ToCursor(default)).Returns(() => CreateCursor(level, rootList));
            //findResult.Setup(f => f.Limit(It.IsAny<int>())).Returns(findResult.Object);
            //roots.Setup(n => n.FindSync(session.Object, It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<FindOptions<MerkleNode>>(), default))
            //    .Returns(() => CreateCursor(level, rootList));

            return client.Object;
        }

        private static AsyncCursor<MerkleNode> CreateNodeCursor(Dictionary<ObjectId, MerkleNode> nodes, ExpressionFilterDefinition<MerkleNode> expr)
        {
            return new AsyncCursor<MerkleNode>(
                channelSource: new Mock<IChannelSource>().Object,
                collectionNamespace: new CollectionNamespace("foo", "bar"),
                query: new BsonDocument(),
                firstBatch: nodes.Values.AsQueryable().Where(expr.Expression).ToArray(),
                cursorId: 0,
                batchSize: null,
                limit: null,
                serializer: new Mock<IBsonSerializer<MerkleNode>>().Object,
                messageEncoderSettings: new MessageEncoderSettings(),
                maxTime: null);
        }

        private static AsyncCursor<MerkleRoot> CreateRootCursor(List<MerkleRoot> roots)
        {
            return new AsyncCursor<MerkleRoot>(
                channelSource: new Mock<IChannelSource>().Object,
                collectionNamespace: new CollectionNamespace("foo", "bar"),
                query: new BsonDocument(),
                firstBatch: new[] { roots.LastOrDefault() },
                cursorId: 0,
                batchSize: null,
                limit: 1,
                serializer: new Mock<IBsonSerializer<MerkleRoot>>().Object,
                messageEncoderSettings: new MessageEncoderSettings(),
                maxTime: null);
        }

    }

    internal class TestLogger<T> : ILogger<MongoDBMerkeTree>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {

        }
    }
}
