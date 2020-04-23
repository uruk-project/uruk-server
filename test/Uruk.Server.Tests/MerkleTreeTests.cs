using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Operations;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;
using Moq;
using Uruk.Server.MongoDB;
using Xunit;

namespace Uruk.Server.Tests
{
    public class MerkleTreeTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(1024)]
        public void CreateTree_Full(int count)
        {
            // Ensures the count parameter is a power of 2
            Assert.Equal(0, (count & (count - 1)));

            var nodeList = new List<MerkleNode>();
            var rootList = new List<MerkleNode>();
            var client = CreateClient(nodeList, rootList);

            var tree = new MongoDBMerkeTree(client, Options.Create(new MongoDBStoreOptions()), new TestLogger<MongoDBMerkeTree>());
            for (int i = 0; i < count; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)i;
                tree.Append(hash);
            }

            Assert.Single(rootList);
            int expectedHeight = (int)Math.Log(count, 2);
            Assert.Equal(expectedHeight, rootList[0].Level);
            foreach (var node in nodeList)
            {
                AssertNodeHash(nodeList, node);
            }
        }

        [Theory]
        [InlineData(3)]
        [InlineData(15)]
        [InlineData(17)]
        [InlineData(31)]
        [InlineData(33)]
        public void CreateTree_Partial(int count)
        {
            // Ensures the count parameter is not a power of 2
            Assert.NotEqual(0, count & (count - 1));

            var nodeList = new List<MerkleNode>();
            var rootList = new List<MerkleNode>();
            var client = CreateClient(nodeList, rootList);

            var tree = new MongoDBMerkeTree(client, Options.Create(new MongoDBStoreOptions()), new TestLogger<MongoDBMerkeTree>());
            for (byte i = 0; i < count; i++)
            {
                var hash = new byte[32];
                hash[0] = i;
                tree.Append(hash);
            }


            Assert.NotEqual(1, rootList.Count);
            int expectedHeight = (int)Math.Log(count, 2);
            Assert.Equal(expectedHeight, rootList[0].Level);
            foreach (var node in nodeList)
            {
                AssertNodeHash(nodeList, node);
            }
        }

        private static void AssertNodeHash(List<MerkleNode> nodeList, MerkleNode node)
        {
            if (node.Left == default && node.Right == default)
            {
                return;
            }

            var left = nodeList.Find(n => n.Id == node.Left);
            Assert.NotNull(left);
            var right = nodeList.Find(n => n.Id == node.Right);
            Assert.NotNull(right);
            var expectedHash = new byte[Sha256.Shared.HashSize];
            Sha256.Shared.ComputeHash(right!.Hash, (ReadOnlySpan<byte>)left!.Hash, expectedHash);
            Assert.Equal(expectedHash, node.Hash);
            Assert.Equal(node.Level - 1, right.Level);
            Assert.Equal(node.Level - 1, left.Level);
        }

        private static IMongoClient CreateClient(List<MerkleNode> nodeList, List<MerkleNode> rootList)
        {
            int level = 0;

            var nodes = new Mock<IMongoCollection<MerkleNode>>(MockBehavior.Strict);
            var roots = new Mock<IMongoCollection<MerkleNode>>(MockBehavior.Strict);
            var db = new Mock<IMongoDatabase>(MockBehavior.Strict);
            var client = new Mock<IMongoClient>(MockBehavior.Strict);
            var session = new Mock<IClientSessionHandle>(MockBehavior.Strict);

            session.Setup(s => s.CommitTransaction(default));

            client.Setup(c => c.GetDatabase(It.IsAny<string>(), null)).Returns(db.Object);
            client.Setup(c => c.StartSession(null, default))
                .Returns(session.Object)
                .Callback(() =>
                {
                    level = 0;
                });

            db.Setup(d => d.GetCollection<MerkleNode>("merkle_roots", null)).Returns(roots.Object);
            db.Setup(d => d.GetCollection<MerkleNode>("merkle_nodes", null)).Returns(nodes.Object);

            nodes.Setup(n => n.InsertOne(session.Object, It.IsAny<MerkleNode>(), null, default))
                .Callback<IClientSessionHandle, MerkleNode, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                {
                    node.Id = ObjectId.GenerateNewId();
                    nodeList.Add(node);
                });

            roots.Setup(n => n.InsertOne(session.Object, It.IsAny<MerkleNode>(), null, default))
                .Callback<IClientSessionHandle, MerkleNode, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                {
                    node.Id = ObjectId.GenerateNewId();
                    rootList.Add(node);
                });

            var findResult = new Mock<IFindFluent<MerkleNode, MerkleNode>>(MockBehavior.Strict);
            findResult.Setup(f => f.ToCursor(default)).Returns(() => CreateCursor(level, rootList));
            findResult.Setup(f => f.Limit(It.IsAny<int>())).Returns(findResult.Object);
            roots.Setup(n => n.FindSync(session.Object, It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<FindOptions<MerkleNode>>(), default))
                .Returns(() => CreateCursor(level, rootList));
            roots.Setup(n => n.CountDocuments(session.Object, It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<CountOptions>(), default))
                .Returns(() => rootList.Count(n => n.Level == level));

            roots.Setup(n => n.DeleteMany(session.Object, It.IsAny<FilterDefinition<MerkleNode>>(), null, default))
                .Returns(() => new DeleteResult.Acknowledged(1 << level))
                .Callback(() =>
                {
                    rootList.RemoveRange(rootList.Count - 2, 2);
                    ++level;
                });

            roots.Setup(n => n.DeleteOne(session.Object, It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), null, default))
                .Returns(new DeleteResult.Acknowledged(1))
                .Callback(() =>
                {
                    rootList.Remove(rootList.First(n => n.Level == 0));
                    level = 1;
                });
            return client.Object;
        }

        private static AsyncCursor<MerkleNode> CreateCursor(int level, List<MerkleNode> rootList)
        {
            return new AsyncCursor<MerkleNode>(
                channelSource: new Mock<IChannelSource>().Object,
                collectionNamespace: new CollectionNamespace("foo", "bar"),
                query: new BsonDocument(),
                firstBatch: rootList.Where(n => n.Level == level).ToArray(),
                cursorId: 0,
                batchSize: null,
                limit: null,
                serializer: new Mock<IBsonSerializer<MerkleNode>>().Object,
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
