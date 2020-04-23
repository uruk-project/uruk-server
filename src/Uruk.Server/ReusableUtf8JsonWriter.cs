using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using JsonWebToken;

namespace Uruk.Server
{
    internal sealed class ReusableUtf8JsonWriter
    {
        [ThreadStatic]
        private static ReusableUtf8JsonWriter? _cachedInstance;

        private readonly Utf8JsonWriter _writer;

#if DEBUG
        private bool _inUse;
#endif

        public ReusableUtf8JsonWriter(IBufferWriter<byte> stream)
        {
            _writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { SkipValidation = true });
        }

        public static ReusableUtf8JsonWriter Get(IBufferWriter<byte> stream)
        {
            var writer = _cachedInstance;
            if (writer == null)
            {
                writer = new ReusableUtf8JsonWriter(stream);
            }

            // Taken off the thread static
            _cachedInstance = null;
#if DEBUG
            if (writer._inUse)
            {
                throw new InvalidOperationException("The writer wasn't returned!");
            }

            writer._inUse = true;
#endif
            writer._writer.Reset(stream);
            return writer;
        }

        public static void Return(ReusableUtf8JsonWriter writer)
        {
            _cachedInstance = writer;

            writer._writer.Reset();

#if DEBUG
            writer._inUse = false;
#endif
        }

        public Utf8JsonWriter GetJsonWriter()
        {
            return _writer;
        }
    }

    //public class MerkleTree
    //{
    //    public static readonly MerkleTree Empty = new MerkleTree(null);

    //    public MerkleNode Root { get; }

    //    public MerkleTree(MerkleNode root)
    //    {
    //        Root = root;
    //    }

    ////    public List<MerkleProofHash> AuditProof(ReadOnlySpan<byte> leafHash)
    ////    {
    ////        List<MerkleProofHash> auditTrail = new List<MerkleProofHash>();

    ////        var leafNode = FindLeaf(leafHash);

    ////        if (leafNode != null)
    ////        {
    ////            var parent = leafNode.Parent;
    ////            BuildAuditTrail(auditTrail, parent, leafNode);
    ////        }

    ////        return auditTrail;
    ////    }

    ////    private void BuildAuditTrail(List<MerkleProofHash> auditTrail, MerkleNode? parent, MerkleNode child)
    ////    {
    ////        if (parent != null)
    ////        {
    ////            var nextChild = parent.IsFirstNode(child) ? parent.RightNode : parent.LeftNode;
    ////            var direction = parent.IsFirstNode(child) ? MerkleProofHash.Branch.Left : MerkleProofHash.Branch.Right;

    ////            // For the last leaf, the right node may not exist.  In that case, we ignore it because it's
    ////            // the hash we are given to verify.
    ////            if (nextChild != null)
    ////            {
    ////                auditTrail.Add(new MerkleProofHash(nextChild.Hash, direction));
    ////            }

    ////            BuildAuditTrail(auditTrail, child.Parent.Parent, child.Parent);
    ////        }
    ////    }

    //}

    //public class InMemoryMerkleTreeBuilder : IMerkleTreeBuilder
    //{
    //    private readonly List<MerkleNode> _trees = new List<MerkleNode>();

    //    public int Count { get; private set; }

    //    private MerkleLeaf? _last;

    //    public void Append(byte[] hash)
    //    {
    //        MerkleLeaf item = new MerkleLeaf(hash);
    //        _last = item;
    //        _trees.Insert(0, item);
    //        Count++;
    //        Compact();
    //    }

    //    public virtual MerkleNode CreateBranch(MerkleNode left, MerkleNode right, int height)
    //    {
    //        return new MerkleBranch(left, right, height);
    //    }

    //    private void Compact()
    //    {
    //        if (_trees.Count < 2)
    //        {
    //            return;
    //        }

    //        for (int i = 0; i < _trees.Count - 1; i++)
    //        {
    //            var tree1 = _trees[i];
    //            var tree2 = _trees[i + 1];

    //            if (tree1.Level == tree2.Level)
    //            {
    //                _trees[i] = CreateBranch(tree1, tree2, tree1.Level + 1);
    //                _trees.RemoveAt(i + 1);
    //                i--;
    //            }
    //        }
    //    }

    //    public MerkleTree Build()
    //    {
    //        if (_trees.Count == 0)
    //        {
    //            return MerkleTree.Empty;
    //        }

    //        if (_trees.Count == 1)
    //        {
    //            return new MerkleTree(_trees[0]);
    //        }

    //        // Fill the tree with the last leaf
    //        // This may be optimized
    //        int requiredLeaves = (int)Math.Pow(2, 1 + (int)Math.Log(Count, 2));
    //        for (int i = Count; i < requiredLeaves; i++)
    //        {
    //            // Is it cheaper to use InsertRange?
    //            _trees.Insert(0, _last);
    //            Count++;
    //            Compact();
    //        }

    //        return new MerkleTree(_trees[0]);
    //    }
    //}

    //public abstract class MerkleNode
    //{
    //    public abstract ReadOnlySpan<byte> Hash { get; }

    //    public abstract int Level { get; }

    //    public MerkleNode? Parent { get; set; }

    //    //public abstract bool IsFirstNode(MerkleNode child);
    //}

    //public class MerkleBranch : MerkleNode
    //{
    //    private readonly byte[] _hash;
    //    private readonly int _height;

    //    public MerkleBranch(MerkleNode left, MerkleNode right, int height)
    //    {
    //        Left = left;
    //        Right = right;
    //        _height = height;
    //        _hash = new byte[Sha256.Shared.HashSize];
    //        Sha256.Shared.ComputeHash(right.Hash, left.Hash, _hash);

    //        right.Parent = left.Parent = this;
    //    }

    //    public override ReadOnlySpan<byte> Hash => _hash;

    //    public MerkleNode Left { get; }

    //    public MerkleNode Right { get; }

    //    public override int Level => _height;

    //    //public override bool IsFirstNode(MerkleNode child) => Left == child;
    //}

    //public class MerkleLeaf : MerkleNode
    //{
    //    private readonly byte[] _hash;

    //    public MerkleLeaf(byte[] hash)
    //    {
    //        _hash = hash;
    //    }

    //    public override ReadOnlySpan<byte> Hash => _hash;

    //    public override int Level => 0;

    //    //public override bool IsFirstNode(MerkleNode child) => false;
    //}

    //public class MerkleProofHash
    //{
    //    public enum Branch
    //    {
    //        Left,
    //        Right,
    //        OldRoot,
    //    }

    //    public byte[] Hash { get; }
    //    public Branch Direction { get; }

    //    public MerkleProofHash(byte[] hash, Branch direction)
    //    {
    //        Hash = hash;
    //        Direction = direction;
    //    }
    //}
}
