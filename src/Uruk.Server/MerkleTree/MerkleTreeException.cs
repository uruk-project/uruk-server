using System;

namespace Uruk.Server
{
    public class MerkleTreeException : Exception
    {
        public MerkleTreeException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public MerkleTreeException(string? message)
        : base(message)
        {
        }
    }
}