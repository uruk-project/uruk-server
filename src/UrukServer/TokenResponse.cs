using System.Text.Json;

namespace UrukServer
{
    public class TokenResponse
    {
        public bool Succeeded { get; set; }

        public JsonEncodedText Error { get; set; }

        public string? Description { get; set; }
    }
}
