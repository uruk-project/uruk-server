using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class FileSystemAuditTrailStore : IAuditTrailStore
    {
        private readonly FileSystemStorageOptions _options;

        public FileSystemAuditTrailStore(IOptions<FileSystemStorageOptions> options)
        {
            _options = options.Value;
        }

        public Task StoreAsync(AuditTrailRecord record, CancellationToken cancellationToken = default)
        {
            // TODO : check for path traversal !
            var path = Path.Combine(_options.Directory, record.ClientId);
            Directory.CreateDirectory(path);
            string id;
            if (record.Token.Payload!.TryGetClaim(JsonWebToken.JwtClaimNames.Jti, out var jti))
            {
                id = jti.GetString()!;
            }
            else
            {
                id = Guid.NewGuid().ToString("n");
            }

            // Warning : Risk of path traversal !
            return File.WriteAllBytesAsync(Path.Combine(path, id), record.Raw.ToArray(), cancellationToken);
        }
    }
}
