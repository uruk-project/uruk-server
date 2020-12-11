using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.AspNetCore.TestHost;
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
    public class MerkleHasherTests
    {
        private const string EmptyHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

        [Fact]
        public void HashEmpty_EmptyHash()
        {
            var hasher = new MerkleHasher(Sha256.Shared);
            var leaf = hasher.HashEmpty();
            Assert.Equal(leaf.ByteToHex(), EmptyHash);
        }

        [Theory]
        [InlineData("", "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D")]
        [InlineData("101112131415161718191A1B1C1D1E1F", "3BFB960453EBAEBF33727DA7A1F4DB38ACC051D381B6DA20D6D4E88F0EABFD7A")]
        public void HashLeaves(string leafHash, string expectedHash)
        {
            var hasher = new MerkleHasher(Sha256.Shared);
            var leaf = hasher.HashLeaf(leafHash.HexToByteArray());
            Assert.Equal(leaf.ByteToHex(), expectedHash);
        }

        [Theory]
        [InlineData("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F", "1A378704C17DA31E2D05B6D121C2BB2C7D76F6EE6FA8F983E596C2D034963C57")]
        public void HashNodes(string left, string right, string expected)
        {
            var hasher = new MerkleHasher(Sha256.Shared);
            var node = hasher.HashNode(left.HexToByteArray(), right.HexToByteArray());
            Assert.Equal(expected, node.ByteToHex());
        }
    }

    public class MerkleTreeTests
    {
        private static readonly IMerkleHasher _hasher = new MerkleHasher(Sha256.Shared);
        private static readonly IMerkleSigner _signer = new TestMerkleSigner();

        private const string EmptyHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

        private static readonly string[][] test_leaves = new[]
        {
            new [] {"" },
            new [] {"", "00" },
            new [] {"", "00", "10" },
            new [] {"", "00", "10", "2021" },
            new [] {"", "00", "10", "2021", "3031" },
            new [] {"", "00", "10", "2021", "3031", "40414243" },
            new [] {"", "00", "10", "2021", "3031", "40414243", "5051525354555657" },
            new [] {"", "00", "10", "2021", "3031", "40414243", "5051525354555657","606162636465666768696A6B6C6D6E6F" }
        };
        public static IEnumerable<object[]> LeavesAndExpectedHash
        {
            get
            {
                yield return new object[] { test_leaves[0], test_vector_hashes[0] };
                yield return new object[] { test_leaves[1], test_vector_hashes[1] };
                yield return new object[] { test_leaves[2], test_vector_hashes[2] };
                yield return new object[] { test_leaves[3], test_vector_hashes[3] };
                yield return new object[] { test_leaves[4], test_vector_hashes[4] };
                yield return new object[] { test_leaves[5], test_vector_hashes[5] };
                yield return new object[] { test_leaves[6], test_vector_hashes[6] };
                yield return new object[] { test_leaves[7], test_vector_hashes[7] };
            }
        }

        private static readonly string[] test_vector_hashes = new[]
        {
            "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D",
            "FAC54203E7CC696CF0DFCB42C92A1D9DBAF70AD9E621F4BD8D98662F00E3C125",
            "AEB6BCFE274B70A14FB067A5E5578264DB0FA9B51AF5E0BA159158F329E06E77",
            "D37EE418976DD95753C1C73862B9398FA2A2CF9B4FF0FDFE8B30CD95209614B7",
            "4E3BBB1F7B478DCFE71FB631631519A3BCA12C9AEFCA1612BFCE4C13A86264D4",
            "76E67DADBCDF1E10E1B74DDC608ABD2F98DFB16FBCE75277B5232A127F2087EF",
            "DDB89BE403809E325750D3D263CD78929C2942B7942A34B77E122C9594A74C8C",
            "5DC9DA79A70659A9AD559CB701DED9A2AB9D823AAD2F4960CFE370EFF4604328"
        };

        public static IEnumerable<object[]> ConsistencyProofs
        {
            get
            {
                yield return new object[]
                {
                    1,
                    1,
                    "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D",
                    "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D",
                    new string[0]
                };
                yield return new object[]
                {
                    1,
                    8,
                    "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D",
                    "5DC9DA79A70659A9AD559CB701DED9A2AB9D823AAD2F4960CFE370EFF4604328",
                    new string[]
                    {
                        "96A296D224F285C67BEE93C30F8A309157F0DAA35DC5B87E410B78630A09CFC7",
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "6B47AAF29EE3C2AF9AF889BC1FB9254DABD31177F16232DD6AAB035CA39BF6E4"
                    }
                };
                yield return new object[]
                {
                    6,
                    8,
                    "76E67DADBCDF1E10E1B74DDC608ABD2F98DFB16FBCE75277B5232A127F2087EF",
                    "5DC9DA79A70659A9AD559CB701DED9A2AB9D823AAD2F4960CFE370EFF4604328",
                    new string[]
                    {
                        "0EBC5D3437FBE2DB158B9F126A1D118E308181031D0A949F8DEDEDEBC558EF6A",
                        "CA854EA128ED050B41B35FFC1B87B8EB2BDE461E9E3B5596ECE6B9D5975A0AE0",
                        "D37EE418976DD95753C1C73862B9398FA2A2CF9B4FF0FDFE8B30CD95209614B7"
                    }
                };
                yield return new object[]
                {
                    2,
                    5,
                    "FAC54203E7CC696CF0DFCB42C92A1D9DBAF70AD9E621F4BD8D98662F00E3C125",
                    "4E3BBB1F7B478DCFE71FB631631519A3BCA12C9AEFCA1612BFCE4C13A86264D4",
                    new string[]
                    {
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "BC1A0643B12E4D2D7C77918F44E0F4F79A838B6CF9EC5B5C283E1F4D88599E6B"
                    }
                };
            }
        }

        public static IEnumerable<object[]> PrecomputedPathTestVectors
        {
            get
            {
                yield return new object[]
                {
                    0, 0, 0,  new string[0]
                };

                yield return new object[]
                {
                    0, 1, 0,  new string[0]
                 };

                yield return new object[]
                {
                    0, 8, 3, new []
                    {
                        "96A296D224F285C67BEE93C30F8A309157F0DAA35DC5B87E410B78630A09CFC7",
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "6B47AAF29EE3C2AF9AF889BC1FB9254DABD31177F16232DD6AAB035CA39BF6E4"}
                    };

                yield return new object[]
                {
                    5, 8, 3, new []
                    {
                        "BC1A0643B12E4D2D7C77918F44E0F4F79A838B6CF9EC5B5C283E1F4D88599E6B",
                        "CA854EA128ED050B41B35FFC1B87B8EB2BDE461E9E3B5596ECE6B9D5975A0AE0",
                        "D37EE418976DD95753C1C73862B9398FA2A2CF9B4FF0FDFE8B30CD95209614B7"
                    }
                };

                yield return new object[]
                {
                    2, 3, 1, new []
                    {
                        "FAC54203E7CC696CF0DFCB42C92A1D9DBAF70AD9E621F4BD8D98662F00E3C125"
                    }
                };

                yield return new object[]
                {
                    1, 5, 3, new []
                    {
                        "6E340B9CFFB37A989CA544E6BB780A2C78901D3FB33738768511A30617AFA01D",
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "BC1A0643B12E4D2D7C77918F44E0F4F79A838B6CF9EC5B5C283E1F4D88599E6B"
                    }
                };
            }
        }

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
                cm.MapCreator(r => new MerkleRoot(r.Level, r.Hash, r.TreeSize, r.Signature, r.Bucket));
            });
            BsonClassMap.RegisterClassMap<MerkleLeaf>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(r => new MerkleLeaf(r.ID, r.Hash));
            });
        }

        [Fact]
        public async Task HashFullAsync_EmptyTree()
        {
            var tree = CreateMerkleTree();
            for (ulong i = 0; i < 5; i++)
            {
                Assert.Equal((await tree.HashFullAsync(i, i)).ByteToHex(), EmptyHash);
            }
        }

        [Fact]
        public async Task HashFullAsync_NonEmptyTree()
        {
            var tree = CreateMerkleTree();
            var l = new byte[5][];
            for (byte i = 0; i < 5; i++)
            {
                var data = new byte[32];
                data[0] = i;
                await tree.AppendAsync(data);
                l[i] = _hasher.HashLeaf(data);
            }

            var rootHash = _hasher.HashNode(_hasher.HashNode(_hasher.HashNode(l[0], l[1]), _hasher.HashNode(l[2], l[3])), l[4]);

            Assert.Equal(await tree.HashFullAsync(0, 5), rootHash);
        }

        [Theory]
        [MemberData(nameof(ConsistencyProofs))]
        public void VerifyConsistency_Precomputed(ulong treeSize1, ulong treeSize2, string root1, string root2, string[] proof)
        {
            var verifier = new MerkleTreeVerifier(_hasher, new TestLogger<MerkleTreeVerifier>());
            var result = verifier.VerifyConsistency(
                treeSize1,
                treeSize2,
                root1.HexToByteArray(),
                root2.HexToByteArray(),
                proof.Select(p => p.HexToByteArray()).ToArray());

            Assert.Equal(IntegrityStatus.Success, result.Status);
        }

        [Fact]
        public async Task VerifyConsistency_Generated()
        {
            var tree = CreateMerkleTree();
            for (ulong i = 0; i < 128; i++)
            {
                var leaf = new byte[32];
                Array.Fill(leaf, (byte)i);
                await tree.AppendAsync(leaf);
            }

            var verifier = new MerkleTreeVerifier(_hasher, new TestLogger<MerkleTreeVerifier>());
            for (ulong i = 1; i < 16; i++)
            {
                var root = await tree.GetRootHashAsync(i);
                for (ulong j = 0; j < i; j++)
                {
                    var consistency_proof = await tree.GetConsistencyProofAsync(j, i);

                    var result = verifier.VerifyConsistency(j, i, await tree.GetRootHashAsync(j), root, consistency_proof.ToArray());
                    Assert.Equal(IntegrityStatus.Success, result.Status);
                }
            }
        }

        [Theory]
        [MemberData(nameof(PrecomputedPathTestVectors))]
        public async Task VerifyInclusion_Precomputed(ulong leaf, ulong treeSizeSnapshot, ulong pathLength, string[] path)
        {
            var tree = await CreateMerkleTreeAsync(TestVector);

            var auditPath = await tree.GetInclusionProofAsync(leaf, treeSizeSnapshot);
            Assert.Equal(auditPath.Count, (int)pathLength);
            Assert.Equal(auditPath.Select(p => p.ByteToHex()).ToArray(), path);

            var leafData = TestVector[(int)leaf];
            var leafHash = _hasher.HashLeaf(leafData.HexToByteArray());
            var hash = await tree.GetRootHashAsync(treeSizeSnapshot);
            var treeHead = new SignedTreeHead(hash, new byte[0], treeSizeSnapshot, "");

            var verifier = new MerkleTreeVerifier(_hasher, new TestLogger<MerkleTreeVerifier>());
            if (treeSizeSnapshot > 0)
            {
                verifier.VerifyInclusion(leafHash, leaf, auditPath.ToArray(), treeHead);
            }
        }

        [Fact]
        public async Task VerifyInclusion_Generated()
        {
            var leafHashes = new List<byte[]>();
            var tree = CreateMerkleTree();
            for (ulong i = 0; i < 128; i++)
            {
                var leaf = new byte[32];
                Array.Fill(leaf, (byte)i);
                await tree.AppendAsync(leaf);
                leafHashes.Add(_hasher.HashLeaf(leaf));
            }

            var verifier = new MerkleTreeVerifier(_hasher, new TestLogger<MerkleTreeVerifier>());
            for (ulong i = 1; i < 16; i++)
            {
                var rootHash = await tree.GetRootHashAsync(i);
                var treeHead = new SignedTreeHead(rootHash, new byte[0], i, "");
                for (ulong j = 0; j < i; j++)
                {
                    var auditPath = await tree.GetInclusionProofAsync(j, i);
                    var result = verifier.VerifyInclusion(leafHashes[(int)j], j, auditPath.ToArray(), treeHead);

                    Assert.Equal(IntegrityStatus.Success, result.Status);
                }
            }
        }

        [Theory]
        [InlineData(0, 11)]
        [InlineData(11, 7)]
        public async Task GetInclusionProofAsync_BadIndices(ulong treeSize1, ulong treeSize2)
        {
            var tree = await CreateMerkleTreeAsync(TestVector);

            var n = await tree.GetTreeSizeAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tree.GetInclusionProofAsync(treeSize1, treeSize2));
        }

        [Theory]
        [InlineData(1, 11)]
        [InlineData(7, 5)]
        public async Task GetConsistencyProofAsync_BadIndices(ulong treeSize1, ulong treeSize2)
        {
            var tree = await CreateMerkleTreeAsync(TestVector);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tree.GetConsistencyProofAsync(treeSize1, treeSize2));
        }

        [Theory]
        [MemberData(nameof(LeavesAndExpectedHash))]
        public async Task HashFullAsync(string[] leaves, string expectedHash)
        {
            var tree = await CreateMerkleTreeAsync(TestVector);

            var result = (await tree.HashFullAsync(0, (ulong)leaves.Length)).ByteToHex();
            Assert.Equal(expectedHash, result);
        }

        [Theory]
        [InlineData(85)]
        public async Task CreateTree(int count)
        {
            var tree = CreateMerkleTree();
            for (int i = 0; i < count; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)i;
                await tree.AppendAsync(hash);
            }

            Assert.Equal(count, tree.Roots.Count);
            for (int i = 0; i < tree.Roots.Count; i++)
            {
                int expectedHeight = (int)Math.Ceiling(Math.Log(i + 1, 2));
                Assert.Equal(expectedHeight, tree.Roots[i].Level);
            }

            for (int i = 0; i < tree.Roots.Count; i++)
            {
                var node = tree.Nodes[tree.Roots[i].Hash];
                Assert.NotNull(node);
                AssertRoot(tree.Nodes, node);
            }
        }

        [Fact]
        public async Task CreateTree_Exception_TransactionAborted()
        {
            var client = CreateFailingClient();
            var tree = new MongoDBMerkleTree(client, _hasher, _signer, Options.Create(new MongoDBStoreOptions()), new TestLogger<MongoDBMerkleTree>());
            var hash = new byte[32];
            await Assert.ThrowsAnyAsync<Exception>(async () => await tree.AppendAsync(hash));
        }

        private static void AssertRoot(Dictionary<byte[], MerkleNode> nodes, MerkleNode node)
        {
            if (node.IsLeaf)
            {
                return;
            }

            var left = nodes[node.Left];
            Assert.NotNull(left);
            var right = nodes[node.Right];
            Assert.NotNull(right);
            Span<byte> expectedHash = _hasher.HashNode(left!.Hash, right!.Hash);
            Assert.True(expectedHash.SequenceEqual(node.Hash));
            Assert.True(node.Level > right.Level);
            Assert.True(node.Level > left.Level);

            AssertRoot(nodes, left);
            AssertRoot(nodes, right);
        }

        [Fact]
        public async Task VerifyIntegrityAsync()
        {
            var tree = CreateMerkleTree();

            for (int i = 0; i < 85; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)i;
                await tree.AppendAsync(hash);
            }

            await tree.VerifyIntegrityAsync();
        }

        [Theory]
        [MemberData(nameof(PrecomputedConsistencyProofs))]
        public async Task GetConsitencyProofAsync(ulong treeSize1, ulong treeSize2, string[] proof)
        {
            var tree = await CreateMerkleTreeAsync(TestVector);

            var consitencyProof = await tree.GetConsistencyProofAsync(treeSize1, treeSize2);

            Assert.Equal(proof, consitencyProof.Select(x => x.ByteToHex()).ToArray());
        }

        [Fact]
        public async Task IncrementalAppend()
        {
            var tree = CreateMerkleTree();

            for (int i = 0; i < TestVector.Length; i++)
            {
                byte[] leaf = TestVector[i].HexToByteArray();
                await tree.AppendAsync(leaf);
                var expectedHash = await tree.HashFullAsync(0, (ulong)i + 1);
                Assert.Equal(expectedHash, tree.Roots[i].Hash);
            }
        }

        private static async Task<MerkleTreeStub> CreateMerkleTreeAsync(string[] testVector)
        {
            var tree = CreateMerkleTree();
            for (int i = 0; i < testVector.Length; i++)
            {
                await tree.AppendAsync(testVector[i].HexToByteArray());
            }

            return tree;
        }

        private static MerkleTreeStub CreateMerkleTree()
        {
            var nodes = new Dictionary<byte[], MerkleNode>();
            var roots = new List<MerkleRoot>();
            var leaves = new List<MerkleLeaf>();
            var client = CreateClient(leaves, nodes, roots);
            return new MerkleTreeStub(client, _hasher, _signer, Options.Create(new MongoDBStoreOptions()), new TestLogger<MongoDBMerkleTree>(), roots, leaves, nodes);
        }

        private class MerkleTreeStub : MongoDBMerkleTree
        {
            public MerkleTreeStub(IMongoClient client, IMerkleHasher hasher, IMerkleSigner signer, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBMerkleTree> logger, List<MerkleRoot> roots, List<MerkleLeaf> leaves, Dictionary<byte[], MerkleNode> nodes)
                : base(client, hasher, signer, options, logger)
            {
                Roots = roots;
                Leaves = leaves;
                Nodes = nodes;
            }

            public List<MerkleRoot> Roots { get; }
            public List<MerkleLeaf> Leaves { get; }
            public Dictionary<byte[], MerkleNode> Nodes { get; }
        }

        private static readonly string[] TestVector = new[]
        {
            "",
            "00",
            "10",
            "2021",
            "3031",
            "40414243",
            "5051525354555657",
            "606162636465666768696a6b6c6d6e6f"
        };

        public static IEnumerable<object[]> PrecomputedConsistencyProofs
        {
            get
            {
                yield return new object[] { 1, 1, new byte[][] { } };
                yield return new object[]
                {
                    1,
                    8,
                    new []
                    {
                        "96A296D224F285C67BEE93C30F8A309157F0DAA35DC5B87E410B78630A09CFC7",
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "6B47AAF29EE3C2AF9AF889BC1FB9254DABD31177F16232DD6AAB035CA39BF6E4"
                    }
                };
                yield return new object[]
                {
                    6,
                    8,
                    new []
                    {
                        "0EBC5D3437FBE2DB158B9F126A1D118E308181031D0A949F8DEDEDEBC558EF6A",
                        "CA854EA128ED050B41B35FFC1B87B8EB2BDE461E9E3B5596ECE6B9D5975A0AE0",
                        "D37EE418976DD95753C1C73862B9398FA2A2CF9B4FF0FDFE8B30CD95209614B7"
                    }
                };
                yield return new object[]
                {
                    2,
                    5,
                    new []
                    {
                        "5F083F0A1A33CA076A95279832580DB3E0EF4584BDFF1F54C8A360F50DE3031E",
                        "BC1A0643B12E4D2D7C77918F44E0F4F79A838B6CF9EC5B5C283E1F4D88599E6B"
                    }
                };
            }
        }

        internal static byte[] HexToByteArray(string hexString, int minimalLength = 0)
        {
            byte[] bytes = new byte[Math.Max(hexString.Length / 2, minimalLength)];

            for (int i = 0; i < hexString.Length; i += 2)
            {
                string s = hexString.Substring(i, 2);
                bytes[i / 2] = byte.Parse(s, NumberStyles.HexNumber, null);
            }

            return bytes;
        }

        private static IMongoClient CreateFailingClient()
        {
            return CreateClient(null, null, null, true);
        }

        private static IMongoClient CreateClient(List<MerkleLeaf>? leaves, Dictionary<byte[], MerkleNode>? nodes, List<MerkleRoot>? roots, bool throwException = false)
        {
            var leafCollection = new Mock<IMongoCollection<MerkleLeaf>>(MockBehavior.Strict);
            var leafIndex = new Mock<IMongoIndexManager<MerkleLeaf>>(MockBehavior.Loose);
            var rootCollection = new Mock<IMongoCollection<MerkleRoot>>(MockBehavior.Strict);
            var rootIndex = new Mock<IMongoIndexManager<MerkleRoot>>(MockBehavior.Loose);
            var nodeCollection = new Mock<IMongoCollection<MerkleNode>>(MockBehavior.Strict);
            var nodeIndex = new Mock<IMongoIndexManager<MerkleNode>>(MockBehavior.Loose);
            var db = new Mock<IMongoDatabase>(MockBehavior.Strict);
            var client = new Mock<IMongoClient>(MockBehavior.Strict);
            var session = new Mock<IClientSessionHandle>(MockBehavior.Strict);

            session.Setup(s => s.CommitTransaction(default));
            if (throwException)
            {
                session.Setup(s => s.StartTransaction(null)).Throws<Exception>();
            }
            else
            {
                session.Setup(s => s.StartTransaction(null));
            }

            client.Setup(c => c.GetDatabase(It.IsAny<string>(), null)).Returns(db.Object);
            client.Setup(c => c.StartSessionAsync(null, default))
                .ReturnsAsync(session.Object);

            db.Setup(d => d.GetCollection<MerkleRoot>("merkle_roots", null)).Returns(rootCollection.Object);
            db.Setup(d => d.GetCollection<MerkleNode>("merkle_nodes", null)).Returns(nodeCollection.Object);
            db.Setup(d => d.GetCollection<MerkleLeaf>("merkle_leaves", null)).Returns(leafCollection.Object);

            rootCollection.SetupGet(r => r.Indexes).Returns(rootIndex.Object);
            if (roots != null)
            {
                rootCollection.Setup(r => r.FindAsync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleRoot>>(), It.IsAny<FindOptions<MerkleRoot>>(), It.IsAny<CancellationToken>()))
                    .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleRoot>, FindOptions<MerkleRoot>, CancellationToken>((s, expr, o, c) => Task.FromResult(CreateRootCursor(roots)));

                rootCollection.Setup(r => r.FindAsync(It.IsAny<ExpressionFilterDefinition<MerkleRoot>>(), It.IsAny<FindOptions<MerkleRoot>>(), It.IsAny<CancellationToken>()))
                    .Returns<ExpressionFilterDefinition<MerkleRoot>, FindOptions<MerkleRoot>, CancellationToken>((expr, o, c) => Task.FromResult(CreateRootCursor(roots)));

                rootCollection.Setup(r => r.FindAsync(It.IsAny<ExpressionFilterDefinition<MerkleRoot>>(), It.IsAny<FindOptions<MerkleRoot>>(), It.IsAny<CancellationToken>()))
                    .Returns<ExpressionFilterDefinition<MerkleRoot>, FindOptions<MerkleRoot>, CancellationToken>((expr, o, c) => Task.FromResult(CreateRootCursor(roots)));

                rootCollection.Setup(n => n.InsertOneAsync(session.Object, It.IsAny<MerkleRoot>(), null, default))
                     .Callback<IClientSessionHandle, MerkleRoot, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                     {
                         roots.Add(node);
                     })
                    .Returns(Task.CompletedTask);
            }

            nodeCollection.SetupGet(r => r.Indexes).Returns(nodeIndex.Object);
            if (nodes != null)
            {
                nodeCollection.Setup(r => r.FindAsync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<FindOptions<MerkleNode>>(), It.IsAny<CancellationToken>()))
                    .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleNode>, FindOptions<MerkleNode>, CancellationToken>((s, expr, o, c) => Task.FromResult(CreateNodeCursor(nodes, expr)));

                nodeCollection.Setup(r => r.FindAsync(It.IsAny<ExpressionFilterDefinition<MerkleNode>>(), It.IsAny<FindOptions<MerkleNode>>(), It.IsAny<CancellationToken>()))
                    .Returns<ExpressionFilterDefinition<MerkleNode>, FindOptions<MerkleNode>, CancellationToken>((expr, o, c) => Task.FromResult(CreateNodeCursor(nodes, expr)));

                nodeCollection.Setup(n => n.InsertOneAsync(session.Object, It.IsAny<MerkleNode>(), null, It.IsAny<CancellationToken>()))
                    .Callback<IClientSessionHandle, MerkleNode, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                    {
                        nodes.Add(node.Hash, node);
                    })
                    .Returns(Task.CompletedTask);

                nodeCollection.Setup(n => n.InsertManyAsync(session.Object, It.IsAny<IEnumerable<MerkleNode>>(), It.IsAny<InsertManyOptions>(), default))
                    .Callback<IClientSessionHandle, IEnumerable<MerkleNode>, InsertManyOptions, CancellationToken>((_, nodesToAdd, __, ___) =>
                    {
                        foreach (var node in nodesToAdd)
                        {
                            nodes.Add(node.Hash, node);
                        }
                    })
                    .Returns(Task.CompletedTask);
            }

            leafCollection.SetupGet(r => r.Indexes).Returns(leafIndex.Object);
            if (leaves != null)
            {
                leafCollection.Setup(r => r.CountDocumentsAsync(Builders<MerkleLeaf>.Filter.Empty, It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
                   .Returns<FilterDefinition<MerkleLeaf>, CountOptions, CancellationToken>((expr, o, c) => Task.FromResult(leaves.LongCount()));

                leafCollection.Setup(r => r.FindAsync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleLeaf>>(), It.IsAny<FindOptions<MerkleLeaf>>(), It.IsAny<CancellationToken>()))
                    .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleLeaf>, FindOptions<MerkleLeaf>, CancellationToken>((s, expr, o, c) => Task.FromResult(CreateLeafCursor(leaves, expr)));

                leafCollection.Setup(r => r.FindAsync(It.IsAny<ExpressionFilterDefinition<MerkleLeaf>>(), It.IsAny<FindOptions<MerkleLeaf, byte[]>>(), It.IsAny<CancellationToken>()))
                    .Returns<ExpressionFilterDefinition<MerkleLeaf>, FindOptions<MerkleLeaf, byte[]>, CancellationToken>((expr, o, c) => Task.FromResult(CreateLeafHashCursor(leaves, expr)));

                leafCollection.Setup(r => r.FindAsync(It.IsAny<IClientSessionHandle>(), It.IsAny<ExpressionFilterDefinition<MerkleLeaf>>(), It.IsAny<FindOptions<MerkleLeaf, byte[]>>(), It.IsAny<CancellationToken>()))
                    .Returns<IClientSessionHandle, ExpressionFilterDefinition<MerkleLeaf>, FindOptions<MerkleLeaf, byte[]>, CancellationToken>((s, expr, o, c) => Task.FromResult(CreateLeafHashCursor(leaves, expr)));

                leafCollection.Setup(n => n.InsertOneAsync(session.Object, It.IsAny<MerkleLeaf>(), null, default))
                     .Callback<IClientSessionHandle, MerkleLeaf, InsertOneOptions, CancellationToken>((_, node, __, ___) =>
                     {
                         leaves.Add(node);
                     })
                    .Returns(Task.CompletedTask);
            }

            return client.Object;
        }

        private static IAsyncCursor<MerkleLeaf> CreateLeafCursor(List<MerkleLeaf> nodes, ExpressionFilterDefinition<MerkleLeaf> expr)
        {
            return new AsyncCursor<MerkleLeaf>(
                channelSource: new Mock<IChannelSource>().Object,
                collectionNamespace: new CollectionNamespace("foo", "bar"),
                query: new BsonDocument(),
                firstBatch: nodes.AsQueryable().Where(expr.Expression).ToArray(),
                cursorId: 0,
                batchSize: null,
                limit: null,
                serializer: new Mock<IBsonSerializer<MerkleLeaf>>().Object,
                messageEncoderSettings: new MessageEncoderSettings(),
                maxTime: null);
        }

        private static IAsyncCursor<byte[]> CreateLeafHashCursor(List<MerkleLeaf> nodes, ExpressionFilterDefinition<MerkleLeaf> expr)
        {
            return new AsyncCursor<byte[]>(
                channelSource: new Mock<IChannelSource>().Object,
                collectionNamespace: new CollectionNamespace("foo", "bar"),
                query: new BsonDocument(),
                firstBatch: nodes.AsQueryable().Where(expr.Expression).Select(l => l.Hash).ToArray(),
                cursorId: 0,
                batchSize: null,
                limit: null,
                serializer: new Mock<IBsonSerializer<byte[]>>().Object,
                messageEncoderSettings: new MessageEncoderSettings(),
                maxTime: null);
        }

        private static IAsyncCursor<MerkleNode> CreateNodeCursor(Dictionary<byte[], MerkleNode> nodes, ExpressionFilterDefinition<MerkleNode> expr)
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

        private static IAsyncCursor<MerkleRoot> CreateRootCursor(List<MerkleRoot> roots)
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

    internal class TestLogger<T> : ILogger<MongoDBMerkleTree>
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

    internal class TestMerkleSigner : IMerkleSigner
    {
        public byte[] Sign(byte[] data)
        {
            return data;
        }
    }
}