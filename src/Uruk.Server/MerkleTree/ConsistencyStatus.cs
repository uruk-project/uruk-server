namespace Uruk.Server
{
    public enum IntegrityStatus
    {
        Success,
        ProofTooShort,
        ProofTooLong,
        DifferentHashSameSize,
        HashMismatch,
    }
}