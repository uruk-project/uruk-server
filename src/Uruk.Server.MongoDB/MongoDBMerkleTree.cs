using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using JsonWebToken.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Uruk.Server.MongoDB
{
    public partial class MongoDBMerkleTree : IMerkleTree
    {
        private readonly IMongoCollection<MerkleRoot> _roots;
        private readonly IMongoCollection<MerkleNode> _nodes;
        private readonly IMongoCollection<MerkleLeaf> _leaves;
        private readonly ILogger<MongoDBMerkleTree> _logger;
        private readonly IMongoClient _client;
        private readonly IMerkleHasher _hasher;
        private readonly IMerkleSigner _signer;
        private readonly MongoDBStoreOptions _options;

        public MongoDBMerkleTree(IMongoClient client, IMerkleHasher hasher, IMerkleSigner signer, IOptions<MongoDBStoreOptions> options, ILogger<MongoDBMerkleTree> logger)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
            _options = options.Value;
            var db = client.GetDatabase(_options.Database);
            _roots = db.GetCollection<MerkleRoot>("merkle_roots");
            _nodes = db.GetCollection<MerkleNode>("merkle_nodes");
            _leaves = db.GetCollection<MerkleLeaf>("merkle_leaves");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var rootIndexModel = new CreateIndexModel<MerkleRoot>(
                    Builders<MerkleRoot>.IndexKeys.Ascending(a => a.Hash)
                );
            _roots.Indexes.CreateOne(rootIndexModel);

            var nodeIndexModel = new CreateIndexModel<MerkleNode>(
                    Builders<MerkleNode>.IndexKeys.Ascending(a => a.Hash)
                );
            _nodes.Indexes.CreateOne(nodeIndexModel);
            var leafIndexModel = new CreateIndexModel<MerkleLeaf>(
                    Builders<MerkleLeaf>.IndexKeys.Ascending(a => a.Hash)
                );
            var leafIdIndexModel = new CreateIndexModel<MerkleLeaf>(
                    Builders<MerkleLeaf>.IndexKeys.Ascending(a => a.ID)
                );
            _leaves.Indexes.CreateMany(new[] { leafIndexModel, leafIdIndexModel });
        }

        public async Task<SignedTreeHead> AppendAsync(byte[] hash)
        {
            MerkleNode leafNode = new MerkleNode(_hasher.HashLeaf(hash));
            var session = await _client.StartSessionAsync();

            try
            {
                //session.StartTransaction();
                var root = await _roots.Find(session, _ => true)
                            .Sort(new SortDefinitionBuilder<MerkleRoot>()
                            .Descending(r => r.TreeSize)).Limit(1).SingleOrDefaultAsync();
                if (root is null)
                {
                    MerkleLeaf leaf = new MerkleLeaf(0, _hasher.HashLeaf(hash));
                    await _leaves.InsertOneAsync(session, leaf);
                    await _nodes.InsertOneAsync(session, leafNode);
                    root = new MerkleRoot(leafNode.Level, leafNode.Hash, 1ul, _signer.Sign(leafNode.Hash), "");
                    await _roots.InsertOneAsync(session, root);
                }
                else
                {
                    var existingLeaf = await _leaves.Find(session, n => n.Hash == leafNode.Hash).AnyAsync();
                    if (existingLeaf)
                    {
                        _logger.LogInformation("The leaf {leafHash} already exist in the tree {treeHash}. Nothing was appended.", hash.ByteToHex(), root.Hash.ByteToHex());
                        return CreateSignedTreeHead(root);
                    }
                    else
                    {
                        var nodesToInsert = new List<MerkleNode>(root.Level + 1)
                        {
                            leafNode
                        };
                        var stack = new Stack<MerkleNode>(root.Level + 1);
                        var currentNode = await _nodes.Find(session, n => n.Hash == root.Hash).SingleAsync();
                        MerkleNode node;
                        if (currentNode.IsFull)
                        {
                            bool isFullNode = currentNode.Level == leafNode.Level && leafNode.IsFull;
                            node = new MerkleNode(new[] { currentNode.Hash, leafNode.Hash }, currentNode.Level + 1, _hasher.HashNode(currentNode.Hash, leafNode.Hash), isFullNode);
                            nodesToInsert.Add(node);
                        }
                        else
                        {
                            do
                            {
                                var left = await _nodes.Find(session, n => n.Hash == currentNode.Left).SingleAsync();
                                stack.Push(left);
                                var right = await _nodes.Find(session, n => n.Hash == currentNode.Right).SingleAsync();
                                currentNode = right;
                            } while (!currentNode.IsFull);

                            stack.Push(currentNode);
                            stack.Push(leafNode);
                            do
                            {
                                var right = stack.Pop();
                                var left = stack.Pop();
                                bool isFullNode = left.Level == right.Level && left.IsFull && right.IsFull;
                                node = new MerkleNode(new[] { left.Hash, right.Hash }, left.Level + 1, _hasher.HashNode(left.Hash, right.Hash), isFullNode);

                                nodesToInsert.Add(node);
                                stack.Push(node);
                            } while (stack.Count != 1);
                        }

                        MerkleLeaf leaf = new MerkleLeaf(root.TreeSize, _hasher.HashLeaf(hash));
                        await _leaves.InsertOneAsync(session, leaf);
                        await _nodes.InsertManyAsync(session, nodesToInsert);
                        root = new MerkleRoot(node.Level, node.Hash, root.TreeSize + 1, _signer.Sign(node.Hash), "");
                        await _roots.InsertOneAsync(session, root);
                    }
                }

                //session.CommitTransaction();
                _logger.LogInformation("The leaf {leafHash} was appended to tree {treeHash}.", hash.ByteToHex(), root.Hash.ByteToHex());
                return CreateSignedTreeHead(root);
            }
            catch (Exception e)
            {
                //session.AbortTransaction();
                _logger.LogError(e, "An error occurred while appending the leaf {leaf}.", hash.ByteToHex());
                throw new MerkleTreeException($"An error occurred while appending the leaf {hash.ByteToHex()}.", e);
            }
        }

        private SignedTreeHead CreateSignedTreeHead(MerkleRoot root)
        {
            return new SignedTreeHead(root.Hash, root.Signature, root.TreeSize, "bucket");
        }

        private static uint Lsb(uint x)
            => x & 1;

        public async Task<bool> VerifyIntegrityAsync()
        {
            try
            {
                MerkleRoot? rootHash = await _roots.Find(_ => true)
                       .Sort(new SortDefinitionBuilder<MerkleRoot>()
                       .Descending(r => r.TreeSize)).Limit(1).SingleOrDefaultAsync();

                if (rootHash == null)
                {
                    return true;
                }

                return await VerifyIntegrityAsync(rootHash.Hash, rootHash.TreeSize);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while verifying the integrity of the tree.");
                throw new MerkleTreeException($"An error occurred while verifying the integrity of the tree.", e);
            }
        }

        private static bool IsPowerOf2(uint x)
            => (x & (x - 1)) == 0;

        public static bool CheckTrees(byte[] first, uint firstTreeSize, byte[] second, uint secondTreeSize, List<byte[]> consistency)
        {
            if (IsPowerOf2(firstTreeSize))
            {
                consistency.Insert(0, first);
            }

            var fn = firstTreeSize - 1;
            var sn = secondTreeSize - 1;

            var tmp = fn >> BitOperations.TrailingZeroCount(fn);
            while (Lsb(fn) != 0)
            {
                fn >>= 1;
                sn >>= 1;
            }

            Debug.Assert(tmp == fn);

            if (consistency.Count == 0)
            {
                return false;
            }

            var fr = consistency[0];
            var sr = consistency[0];

            for (int i = 1; i < consistency.Count; i++)
            {
                var c = consistency[i];
                if (sn == 0)
                {
                    return false;
                }

                if (Lsb(fn) != 0 || fn == sn)
                {
                    Sha256.Hash(fr, c, fr);
                    Sha256.Hash(sr, c, sr);
                    while (!(fn == 0 || Lsb(fn) != 0))
                    {
                        fn >>= 1;
                        sn >>= 1;
                    }
                }
                else
                {
                    Sha256.Hash(c, sr, sr);
                }

                fn >>= 1;
                sn >>= 1;
            }

            return fr.AsSpan().SequenceEqual(first) && sr.AsSpan().SequenceEqual(second);
        }

        public async Task<List<byte[]>> GetConsistencyProofAsync(ulong treeSize1, ulong treeSize2 = 0)
        {
            var currentTreeSize = await GetTreeSizeAsync();

            if (treeSize2 == 0)
            {
                treeSize2 = currentTreeSize;
            }

            if (treeSize1 > treeSize2)
            {
                throw new ArgumentOutOfRangeException($"{nameof(treeSize1)} must be less than {nameof(treeSize2)}.", nameof(treeSize1));
            }

            if (treeSize1 > currentTreeSize || treeSize2 > currentTreeSize)
            {
                throw new ArgumentOutOfRangeException($"Requested proof for sizes beyond current tree: current tree: {currentTreeSize}, tree size 1: {treeSize1}, tree size 2: {treeSize2}.");
            }

            if (treeSize1 == treeSize2 || treeSize1 == 0)
            {
                return new List<byte[]>();
            }

            return await CalculateSubProof(treeSize1, 0, treeSize2, true);
        }

        private async Task<List<byte[]>> CalculateSubProof(ulong treeSize1, ulong start, ulong end, bool completeSubtree)
        {
            ulong n = end - start;
            ulong m = treeSize1;
            if (m == n || n == 1)
            {
                if (completeSubtree)
                {
                    return new List<byte[]>();
                }
                else
                {
                    return new List<byte[]> { await HashFullAsync(start, end) };
                }
            }

            List<byte[]> res;
            var k = NearestLowerPowerOf2(n);
            byte[] node;
            if (m <= k)
            {
                node = await HashFullAsync(start + k, start + n);
                res = await CalculateSubProof(m, start, start + k, completeSubtree);
            }
            else
            {
                node = await HashFullAsync(start, start + k);
                res = await CalculateSubProof(m - k, start + k, start + n, false);
            }

            res.Add(node);
            return res;
        }

        public async Task<byte[]> HashFullAsync(ulong leftIndex, ulong rightIndex)
        {
            if (rightIndex < leftIndex)
            {
                throw new InvalidOperationException();
            }

            ulong width = rightIndex - leftIndex;
            if (width == 0)
            {
                return _hasher.HashEmpty();
            }

            if (width == 1)
            {
                var leaf = await _leaves.Find(l => l.ID == leftIndex).Project(l => l.Hash).SingleOrDefaultAsync();
                if (leaf is null)
                {
                    throw new MerkleTreeException("");
                }

                return leaf;
            }

            var splitWidth = 1u << BitLength(width - 1) - 1;
            Debug.Assert(splitWidth < width);
            Debug.Assert(width <= 2 * splitWidth);
            var leftHash = await HashFullAsync(leftIndex, leftIndex + splitWidth);
            var rightHash = await HashFullAsync(leftIndex + splitWidth, rightIndex);
            var hash = _hasher.HashNode(leftHash, rightHash);

            return hash;
        }

        private static uint NearestLowerPowerOf2(ulong x)
        {
            return 1u << (sizeof(ulong) * 8 - BitOperations.LeadingZeroCount(x - 1)) - 1;
        }

        private static int BitLength(ulong bits)
        {
            return sizeof(ulong) * 8 - BitOperations.LeadingZeroCount(bits);
        }

        public async Task<ulong> GetTreeSizeAsync()
        {
            return (ulong)await _leaves.CountDocumentsAsync(Builders<MerkleLeaf>.Filter.Empty);
        }

        public async Task<List<byte[]>> CalculateInclusionProof(ulong start, ulong end, ulong leafIndex)
        {
            var n = end - start;
            if (n == 0 || n == 1)
            {
                return new List<byte[]>();
            }

            var k = NearestLowerPowerOf2(n);
            var m = leafIndex;
            List<byte[]> path;
            if (m < k)
            {
                var treeHead = await HashFullAsync(start + k, start + n);
                path = await CalculateInclusionProof(start, start + k, m);
                path.Add(treeHead);
            }
            else
            {
                var treeHead = await HashFullAsync(start, start + k);
                path = await CalculateInclusionProof(start + k, start + n, m - k);
                path.Add(treeHead);
            }

            return path;
        }

        public async Task<List<byte[]>> GetInclusionProofAsync(ulong leafIndex, ulong treeSize)
        {
            var currentTreeSize = await GetTreeSizeAsync();
            if (treeSize > currentTreeSize)
            {
                throw new ArgumentOutOfRangeException($"Specified tree size is beyond known tree: {treeSize}.", nameof(treeSize));
            }

            if (leafIndex >= currentTreeSize)
            {
                throw new ArgumentOutOfRangeException($"Requested proof for leaf beyond tree size: {leafIndex}.", nameof(leafIndex));
            }

            return await CalculateInclusionProof(0, treeSize, leafIndex);
        }

        public async Task<byte[]> GetRootHashAsync(ulong tree_size = 0)
        {
            var currentTreeSize = await GetTreeSizeAsync();
            if (tree_size == 0)
            {
                tree_size = currentTreeSize;
            }

            if (tree_size > currentTreeSize)
            {
                throw new InvalidOperationException($"Specified size beyond known tree: {currentTreeSize}.");
            }

            return await HashFullAsync(0, tree_size);
        }

        private async Task<bool> VerifyIntegrityAsync(byte[] rootHash, ulong treeSize)
        {
            var expectedTree = await GenerateTreeHashAsync(treeSize);

            return rootHash.AsSpan().SequenceEqual(expectedTree);
        }

        private async ValueTask<byte[]> GenerateTreeHashAsync(ulong treeSize)
        {
            var stack = new Stack<byte[]>();
            var cursor = await _nodes.Find(_ => true, new FindOptions { BatchSize = 256 }).ToCursorAsync();
            int mergeCount = 0;
            ulong i = 0;
            while (await cursor.MoveNextAsync() && i < treeSize)
            {
                foreach (var node in cursor.Current)
                {
                    stack.Push(node.Hash);
                    mergeCount = BitOperations.TrailingZeroCount(i ^ 0xFFFFFFFFFFFFFFFF);

                    for (int j = 0; j < mergeCount; j++)
                    {
                        Merge(stack);
                    }

                    i++;
                }
            }

            while (stack.Count > 1)
            {
                Merge(stack);
            }

            return stack.Pop();
        }

        private void Merge(Stack<byte[]> stack)
        {
            var right = stack.Pop();
            var left = stack.Pop();
            var h = _hasher.HashNode(left, right);
            stack.Push(h);
        }
    }
}