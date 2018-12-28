using System;
using Newtonsoft.Json;
using ProtoBuf;

namespace Laobian.Blog.Log
{
    [ProtoContract]
    public class BlogLog
    {
        [JsonIgnore]
        public Guid Id { get; set; }

        [ProtoMember(1)]
        [JsonProperty("via")]
        public string Via { get; set; }

        [ProtoMember(2)]
        [JsonProperty("host")]
        public string Host { get; set; }

        [ProtoMember(3)]
        [JsonProperty("userAgent")]
        public string UserAgent { get; set; }

        [ProtoMember(4)]
        [JsonProperty("cookie")]
        public string Cookie { get; set; }

        [ProtoMember(5)]
        [JsonProperty("acceptLanguage")]
        public string AcceptLanguage { get; set; }

        [ProtoMember(6)]
        [JsonProperty("accept")]
        public string Accept { get; set; }

        [ProtoMember(7)]
        [JsonProperty("referer")]
        public string Referer { get; set; }

        [ProtoMember(8)]
        [JsonProperty("remoteIp")]
        public string RemoteIp { get; set; }

        [ProtoMember(9)]
        [JsonProperty("fullUrl")]
        public string FullUrl { get; set; }

        [ProtoMember(10)]
        [JsonProperty("when")]
        public DateTime When { get; set; } = DateTime.UtcNow;

        [ProtoMember(11)]
        [JsonProperty("machine")]
        public string MachineName { get; set; } = Environment.MachineName;

        [ProtoMember(12)]
        [JsonProperty("user")]
        public string User { get; set; } = Environment.UserName;
    }
}
