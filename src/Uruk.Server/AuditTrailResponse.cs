﻿using System.Text.Json;

namespace Uruk.Server
{
    public class AuditTrailResponse
    {
        public bool Succeeded { get; set; }

        public JsonEncodedText Error { get; set; }

        public string? Description { get; set; }
    }
}