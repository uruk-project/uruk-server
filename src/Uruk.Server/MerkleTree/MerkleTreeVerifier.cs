using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Uruk.Server
{
    public class MerkleTreeVerifier
    {
        private readonly IMerkleHasher _hasher;
        private readonly ILogger _logger;

        public MerkleTreeVerifier(IMerkleHasher hasher, ILogger logger)
        {
            _hasher = hasher;
            _logger = logger;
        }

        public IntegrityResult VerifyConsistency(ulong oldSize, ulong newSize, byte[] oldRoot, byte[] newRoot, byte[][] proof)
        {
            if (oldSize < 0)
            {
                throw new ArgumentOutOfRangeException("Negative tree size", nameof(oldSize));
            }

            if (newSize < 0)
            {
                throw new ArgumentOutOfRangeException("Negative tree size", nameof(newSize));
            }

            if (oldSize > newSize)
            {
                throw new ArgumentOutOfRangeException($"Older tree has bigger size ({oldSize} vs {newSize}), did you supply inputs in the wrong order?", nameof(oldSize));
            }

            if (oldSize == newSize)
            {
                if (oldRoot.SequenceEqual(newRoot))
                {
                    if (proof.Length != 0)
                    {
                        _logger.LogWarning("Trees are identical, ignoring proof");
                    }

                    return IntegrityResult.Succeeded();
                }
                else
                {
                    return IntegrityResult.DifferentHashSameSize();
                }
            }

            if (oldSize == 0)
            {
                if (proof.Length != 0)
                {
                    return IntegrityResult.ProofTooLong();
                }

                return IntegrityResult.Succeeded();
            }

            var node = oldSize - 1;
            var lastNode = newSize - 1;

            while ((node & 1) != 0)
            {
                node /= 2;
                lastNode /= 2;
            }

            var proofQueue = new Queue<byte[]>(proof);
            while (proofQueue.Count != 0)
            {
                byte[] p;

                byte[] computedOldHash;
                byte[] computedNewHash;
                byte[] nextHode;
                if (node != 0)
                {
                    if (!proofQueue.TryDequeue(out p!))
                    {
                        return IntegrityResult.ProofTooShort();
                    }

                    computedNewHash = computedOldHash = p;
                }
                else
                {
                    computedNewHash = computedOldHash = oldRoot;
                }

                while (node != 0)
                {
                    if ((node & 1) != 0)
                    {
                        if (!proofQueue.TryDequeue(out p!))
                        {
                            return IntegrityResult.ProofTooShort();
                        }

                        nextHode = p;
                        computedOldHash = _hasher.HashNode(nextHode, computedOldHash);
                        computedNewHash = _hasher.HashNode(nextHode, computedNewHash);
                    }
                    else if (node < lastNode)
                    {
                        if (!proofQueue.TryDequeue(out p!))
                        {
                            return IntegrityResult.ProofTooShort();
                        }

                        computedNewHash = _hasher.HashNode(computedNewHash, p);
                    }

                    node /= 2;
                    lastNode /= 2;
                }

                while (lastNode != 0)
                {
                    if (!proofQueue.TryDequeue(out p!))
                    {
                        return IntegrityResult.ProofTooShort();
                    }

                    computedNewHash = _hasher.HashNode(computedNewHash, p);
                    lastNode /= 2;
                }

                if (!computedNewHash.SequenceEqual(newRoot))
                {
                    return IntegrityResult.HashMismatch(newRoot, computedNewHash);
                }
                else if (!computedOldHash.SequenceEqual(oldRoot))
                {
                    return IntegrityResult.HashMismatch(oldRoot, computedOldHash);
                }
            }

            if (proofQueue.Count > 0)
            {
                return IntegrityResult.ProofTooLong();
            }

            return IntegrityResult.Succeeded();
        }

        public IntegrityResult VerifyInclusion(byte[] leafHash, ulong leafIndex, byte[][] proof, SignedTreeHead head)
        {
            if (head.TreeSize <= leafIndex)
            {
                throw new ArgumentOutOfRangeException($"Provided STH is for a tree that is smaller than the leaf index. Tree size: {head.TreeSize} Leaf index: {leafIndex}", nameof(head));
            }

            if (head.TreeSize < 0)
            {
                throw new ArgumentOutOfRangeException($"Negative tree size: {head.TreeSize}", nameof(head));
            }

            if (leafIndex < 0)
            {
                throw new ArgumentOutOfRangeException($"Negative leaf index: {leafIndex}", nameof(leafIndex));
            }

            ulong nodeIndex = leafIndex;
            var calculatedHash = leafHash;
            var lastNode = head.TreeSize - 1;
            var queue = new Queue<byte[]>(proof);

            while (lastNode > 0)
            {
                if (queue.Count == 0)
                {
                    return IntegrityResult.ProofTooShort();
                }

                if ((nodeIndex & 1) != 0)
                {
                    var auditHash = queue.Dequeue();
                    calculatedHash = _hasher.HashNode(auditHash, calculatedHash);
                }
                else if (nodeIndex < lastNode)
                {
                    var audit_hash = queue.Dequeue();
                    calculatedHash = _hasher.HashNode(calculatedHash, audit_hash);
                }

                nodeIndex /= 2;
                lastNode /= 2;
            }

            if (queue.Count != 0)
            {
                return IntegrityResult.ProofTooLong();
                throw new MerkleTreeException($"Proof too long: left with {queue.Count} hashes.");
            }

            if (calculatedHash.SequenceEqual(head.Hash))
            {
                return IntegrityResult.Succeeded();
            }

            return IntegrityResult.HashMismatch(head.Hash, calculatedHash);
        }
    }
}