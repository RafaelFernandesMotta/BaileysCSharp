﻿using Newtonsoft.Json;

namespace WhatsSocket.Core.Models.Sessions
{
    public class IndexInfo
    {
        [JsonProperty("baseKey")]
        public byte[] BaseKey { get; set; }

        [JsonProperty("baseKeyType")]
        public int BaseKeyType { get; set; }

        [JsonProperty("closed")]
        public int Closed { get; set; }

        [JsonProperty("used")]
        public long Used { get; set; }

        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("remoteIdentityKey")]
        public byte[] RemoteIdentityKey { get; set; }
    }
}
