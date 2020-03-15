using System.Buffers;
using System.Threading.Tasks;
using JsonWebToken;

namespace UrukServer
{
    public interface IEventReceiverService
    {
        public Task<TokenResponse> TryStoreToken(ReadOnlySequence<byte> buffer, TokenValidationPolicy policy);
    }
}
