using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class DefaultAuditTrailStore : IAuditTrailStore
    {
        private readonly AuditTrailHubOptions _options;
        private readonly string _directory;

        public DefaultAuditTrailStore(IOptions<AuditTrailHubOptions> options)
        {
            _options = options.Value;
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _directory = Path.Combine(root, ".uruk-server");
        }

        public ValueTask<bool> CheckDuplicateAsync(string issuer, string id, string clientId)
        {
            var path = Path.Combine(_directory, clientId, id);
            return new ValueTask<bool>(File.Exists(path));
        }

        public Task StoreAsync(AuditTrailRecord record)
        {
            var path = Path.Combine(_directory, record.ClienId);
            Directory.CreateDirectory(path);
            return File.WriteAllBytesAsync(Path.Combine(path, record.Token.Id!), record.Raw);
        }
    }
}
