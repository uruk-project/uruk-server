using System.ComponentModel;
using System.Diagnostics;

namespace Uruk.Server
{
    [DebuggerDisplay("{DebuggerDisplay(),nq}")]
    public sealed class IntegrityResult
    {
        private static readonly IntegrityResult _success = new IntegrityResult(IntegrityStatus.Success);
        private static readonly IntegrityResult _proofTooShort = new IntegrityResult(IntegrityStatus.ProofTooShort);
        private static readonly IntegrityResult _proofTooLong = new IntegrityResult(IntegrityStatus.ProofTooLong);
        private static readonly IntegrityResult _differentHashSameSize = new IntegrityResult(IntegrityStatus.DifferentHashSameSize);

        private IntegrityResult(IntegrityStatus status)
        {
            Status = status;
        }

        public IntegrityResult(byte[] hash, byte[] computedHash)
        {
            Status = IntegrityStatus.HashMismatch;
            ExpectedHash = hash;
            ComputedHash = computedHash;
        }

        public bool Success => Status == IntegrityStatus.Success;
        public IntegrityStatus Status { get; }
        public byte[]? ExpectedHash { get; }
        public byte[]? ComputedHash { get; }

        public static IntegrityResult Succeeded() => _success;

        public static IntegrityResult ProofTooShort() => _proofTooShort;
        public static IntegrityResult ProofTooLong() => _proofTooLong;

        public static IntegrityResult DifferentHashSameSize() => _differentHashSameSize;

        public static IntegrityResult HashMismatch(byte[] expectedHash, byte[] computedHash)
            => new IntegrityResult(expectedHash, computedHash);

        private string DebuggerDisplay()
        {
            return Status switch
            {
                IntegrityStatus.Success => "Success.",
                IntegrityStatus.ProofTooShort => "The proof is too short.",
                IntegrityStatus.ProofTooLong => "The proof is too long.",
                IntegrityStatus.DifferentHashSameSize => "Different root hashes for the same tree size.",
                IntegrityStatus.HashMismatch => $"Root hash does not match. Expected hash: {ExpectedHash!.ByteToHex()}, computed hash: {ComputedHash!.ByteToHex()}",
                _ => throw new InvalidEnumArgumentException(nameof(Status), (int)Status, Status.GetType())
            };
        }
    }
}